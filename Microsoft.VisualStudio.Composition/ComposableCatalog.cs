namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;

    public class ComposableCatalog : IEquatable<ComposableCatalog>
    {
        /// <summary>
        /// The types behind the parts in the catalog.
        /// </summary>
        private ImmutableHashSet<Type> types;

        /// <summary>
        /// The parts in the catalog.
        /// </summary>
        private ImmutableHashSet<ComposablePartDefinition> parts;

        private ImmutableDictionary<string, ImmutableList<ExportDefinitionBinding>> exportsByContract;

        private ComposableCatalog(ImmutableHashSet<Type> types, ImmutableHashSet<ComposablePartDefinition> parts, ImmutableDictionary<string, ImmutableList<ExportDefinitionBinding>> exportsByContract, DiscoveredParts discoveredParts)
        {
            Requires.NotNull(types, "types");
            Requires.NotNull(parts, "parts");
            Requires.NotNull(exportsByContract, "exportsByContract");
            Requires.NotNull(discoveredParts, "discoveredParts");

            this.types = types;
            this.parts = parts;
            this.exportsByContract = exportsByContract;
            this.DiscoveredParts = discoveredParts;
        }

        public IReadOnlyList<ExportDefinitionBinding> GetExports(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            // We always want to consider exports with a matching contract name.
            var exports = this.exportsByContract.GetValueOrDefault(importDefinition.ContractName, ImmutableList.Create<ExportDefinitionBinding>());

            // For those imports of generic types, we also want to consider exports that are based on open generic exports,
            string genericTypeDefinitionContractName;
            Type[] genericTypeArguments;
            if (TryGetOpenGenericExport(importDefinition, out genericTypeDefinitionContractName, out genericTypeArguments))
            {
                var openGenericExports = this.exportsByContract.GetValueOrDefault(genericTypeDefinitionContractName, ImmutableList.Create<ExportDefinitionBinding>());

                // We have to synthesize exports to match the required generic type arguments.
                exports = exports.AddRange(
                    from export in openGenericExports
                    select export.CloseGenericExport(genericTypeArguments));
            }

            var filteredExports = from export in exports
                                  where importDefinition.ExportConstraints.All(c => c.IsSatisfiedBy(export.ExportDefinition))
                                  select export;

            return ImmutableList.CreateRange(filteredExports);
        }

        internal static bool TryGetOpenGenericExport(ImportDefinition importDefinition, out string contractName, out Type[] typeArguments)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            // TODO: if the importer isn't using a customized contract name.
            if (importDefinition.Metadata.TryGetValue(CompositionConstants.GenericContractMetadataName, out contractName) &&
                importDefinition.Metadata.TryGetValue(CompositionConstants.GenericParametersMetadataName, out typeArguments))
            {
                return true;
            }

            contractName = null;
            typeArguments = null;
            return false;
        }

        /// <summary>
        /// Gets the assemblies within which parts are defined.
        /// </summary>
        public IEnumerable<Assembly> Assemblies
        {
            get { return this.types.Select(t => t.GetTypeInfo().Assembly).Distinct(); }
        }

        /// <summary>
        /// Gets the set of parts that belong to the catalog.
        /// </summary>
        public IImmutableSet<ComposablePartDefinition> Parts
        {
            get { return this.parts; }
        }

        /// <summary>
        /// Gets the parts that were added to the catalog via a <see cref="PartDiscovery"/> class.
        /// </summary>
        public DiscoveredParts DiscoveredParts { get; private set; }

        public static ComposableCatalog Create()
        {
            return new ComposableCatalog(
                ImmutableHashSet.Create<Type>(),
                ImmutableHashSet.Create<ComposablePartDefinition>(),
                ImmutableDictionary.Create<string, ImmutableList<ExportDefinitionBinding>>(),
                DiscoveredParts.Empty);
        }

        public static ComposableCatalog Create(IEnumerable<ComposablePartDefinition> parts)
        {
            Requires.NotNull(parts, "parts");
            return Create().WithParts(parts);
        }

        public static ComposableCatalog Create(DiscoveredParts parts)
        {
            Requires.NotNull(parts, "parts");
            return Create().WithParts(parts);
        }

        public ComposableCatalog WithPart(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, "partDefinition");

            var parts = this.parts.Add(partDefinition);
            if (parts == this.parts)
            {
                // This part is already in the catalog.
                return this;
            }

            var types = this.types.Add(partDefinition.Type);
            var exportsByContract = this.exportsByContract;

            foreach (var exportDefinition in partDefinition.ExportedTypes)
            {
                var list = exportsByContract.GetValueOrDefault(exportDefinition.ContractName, ImmutableList.Create<ExportDefinitionBinding>());
                exportsByContract = exportsByContract.SetItem(exportDefinition.ContractName, list.Add(new ExportDefinitionBinding(exportDefinition, partDefinition, default(MemberRef))));
            }

            foreach (var exportPair in partDefinition.ExportingMembers)
            {
                var member = exportPair.Key;
                foreach (var export in exportPair.Value)
                {
                    var list = exportsByContract.GetValueOrDefault(export.ContractName, ImmutableList.Create<ExportDefinitionBinding>());
                    exportsByContract = exportsByContract.SetItem(export.ContractName, list.Add(new ExportDefinitionBinding(export, partDefinition, member)));
                }
            }

            return new ComposableCatalog(types, parts, exportsByContract, this.DiscoveredParts);
        }

        public ComposableCatalog WithParts(IEnumerable<ComposablePartDefinition> parts)
        {
            Requires.NotNull(parts, "parts");

            // PERF: This has shown up on ETL traces as inefficient and expensive
            //       WithPart should call WithParts instead, and WithParts should
            //       execute a more efficient batch operation.
            return parts.Aggregate(this, (catalog, part) => catalog.WithPart(part));
        }

        public ComposableCatalog WithParts(DiscoveredParts parts)
        {
            Requires.NotNull(parts, "parts");

            var catalog = this.WithParts(parts.Parts);
            return new ComposableCatalog(catalog.types, catalog.parts, catalog.exportsByContract, catalog.DiscoveredParts.Merge(parts));
        }

        public IReadOnlyCollection<AssemblyName> GetInputAssemblies()
        {
            var inputAssemblies = ImmutableHashSet.CreateBuilder<AssemblyName>();
            foreach (var part in this.Parts)
            {
                part.GetInputAssemblies(inputAssemblies);
            }

            return inputAssemblies.ToImmutable();
        }

        public bool Equals(ComposableCatalog other)
        {
            if (other == null)
            {
                return false;
            }

            // A catalog is just the sum of its parts. Anything else is a side-effect of how it was discovered,
            // which shouldn't impact an equivalence check.
            bool result = this.parts.SetEquals(other.parts);
            return result;
        }

        public override int GetHashCode()
        {
            int hashCode = this.Parts.Count;
            foreach (var part in this.Parts)
            {
                hashCode += part.GetHashCode();
            }

            return hashCode;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            using (indentingWriter.Indent())
            {
                foreach (var part in this.parts)
                {
                    indentingWriter.WriteLine("Part");
                    using (indentingWriter.Indent())
                    {
                        part.ToString(indentingWriter);
                    }
                }
            }
        }
    }
}
