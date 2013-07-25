namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    public class ComposableCatalog
    {
        private ImmutableHashSet<Type> types;

        private ImmutableHashSet<ComposablePartDefinition> parts;

        private ImmutableDictionary<CompositionContract, ImmutableList<Export>> exportsByContract;

        private ComposableCatalog(ImmutableHashSet<Type> types, ImmutableHashSet<ComposablePartDefinition> parts, ImmutableDictionary<CompositionContract, ImmutableList<Export>> exportsByContract)
        {
            Requires.NotNull(types, "types");
            Requires.NotNull(parts, "parts");
            Requires.NotNull(exportsByContract, "exportsByContract");

            this.types = types;
            this.parts = parts;
            this.exportsByContract = exportsByContract;
        }

        public IReadOnlyList<Export> GetExports(ImportDefinition import)
        {
            Requires.NotNull(import, "import");

            var exports = this.exportsByContract.GetValueOrDefault(import.Contract, ImmutableList.Create<Export>());

            if (import.Contract.Type.IsGenericType && !import.Contract.Type.IsGenericTypeDefinition)
            {
                var typeDefinitionContract = new CompositionContract(import.Contract.ContractName, import.Contract.Type.GetGenericTypeDefinition());
                exports = exports.AddRange(this.exportsByContract.GetValueOrDefault(typeDefinitionContract, ImmutableList.Create<Export>()));
            }


            var filteredExports = from export in exports
                                  where HasCompatibleCreationPolicies(export.PartDefinition, import)
                                  where HasMetadata(export.ExportDefinition, GetRequiredMetadata(import))
                                  where import.ExportContraints.All(c => c.IsSatisfiedBy(import, export.ExportDefinition))
                                  select export;

            return ImmutableList.From(filteredExports);
        }

        public IEnumerable<Assembly> Assemblies
        {
            get { return this.types.Select(t => t.Assembly).Distinct(); }
        }

        public IImmutableSet<ComposablePartDefinition> Parts
        {
            get { return this.parts; }
        }

        public static ComposableCatalog Create()
        {
            return new ComposableCatalog(
                ImmutableHashSet.Create<Type>(),
                ImmutableHashSet.Create<ComposablePartDefinition>(),
                ImmutableDictionary.Create<CompositionContract, ImmutableList<Export>>());
        }

        public static ComposableCatalog Create(IEnumerable<ComposablePartDefinition> parts)
        {
            return parts.Aggregate(Create(), (catalog, part) => catalog.WithPart(part));
        }

        public static ComposableCatalog Create(IEnumerable<Type> types, PartDiscovery discovery = null)
        {
            Requires.NotNull(types, "types");
            discovery = discovery ?? new AttributedPartDiscovery();

            return Create(types.Select(discovery.CreatePart).Where(p => p != null));
        }

        public static ComposableCatalog Create(params Type[] types)
        {
            Requires.NotNull(types, "types");
            return Create((IEnumerable<Type>)types);
        }

        public ComposableCatalog WithPart(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, "partDefinition");

            var types = this.types.Add(partDefinition.Type);
            var parts = this.parts.Add(partDefinition);
            var exportsByContract = this.exportsByContract;

            foreach (var export in partDefinition.ExportedTypes)
            {
                var list = exportsByContract.GetValueOrDefault(export.Contract, ImmutableList.Create<Export>());
                exportsByContract = exportsByContract.SetItem(export.Contract, list.Add(new Export(export, partDefinition, exportingMember: null)));
            }

            foreach (var exportPair in partDefinition.ExportingMembers)
            {
                var member = exportPair.Key;
                var export = exportPair.Value;
                var list = exportsByContract.GetValueOrDefault(export.Contract, ImmutableList.Create<Export>());
                exportsByContract = exportsByContract.SetItem(export.Contract, list.Add(new Export(export, partDefinition, member)));
            }

            return new ComposableCatalog(types, parts, exportsByContract);
        }

        private static bool HasMetadata(ExportDefinition exportDefinition, IReadOnlyCollection<string> metadataNames)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");
            Requires.NotNull(metadataNames, "metadataNames");

            return metadataNames.All(name => exportDefinition.Metadata.ContainsKey(name));
        }

        private static bool HasCompatibleCreationPolicies(ComposablePartDefinition exportPartDefinition, ImportDefinition importDefinition)
        {
            return exportPartDefinition.CreationPolicy == MefV1.CreationPolicy.Any
                || importDefinition.RequiredCreationPolicy == MefV1.CreationPolicy.Any
                || exportPartDefinition.CreationPolicy == importDefinition.RequiredCreationPolicy;
        }

        private static IReadOnlyCollection<string> GetRequiredMetadata(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            var requiredMetadata = ImmutableHashSet.CreateBuilder<string>();

            if (importDefinition.MetadataType != null)
            {
                if (importDefinition.MetadataType.IsInterface && !importDefinition.MetadataType.IsEquivalentTo(typeof(IDictionary<string, object>)))
                {
                    foreach (var property in importDefinition.MetadataType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (property.GetCustomAttribute<DefaultValueAttribute>() == null)
                        {
                            requiredMetadata.Add(property.Name);
                        }
                    }
                }
            }

            return requiredMetadata.ToImmutable();
        }
    }
}
