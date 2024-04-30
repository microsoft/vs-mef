﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using MessagePack;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using MessagePack.Formatters;

    //[MessagePackObject(true)]
    [MessagePackFormatter(typeof(ComposableCatalogFormatter))]

    public class ComposableCatalog : IEquatable<ComposableCatalog>
    {
        class ComposableCatalogFormatter : IMessagePackFormatter<ComposableCatalog>
        {
            public ComposableCatalog Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var ComposablePartDefinition = options.Resolver.GetFormatterWithVerify<IImmutableSet<ComposablePartDefinition>>().Deserialize(ref reader, options);
                var Resolver = options.Resolver.GetFormatterWithVerify<Resolver>().Deserialize(ref reader, options);

              //  var parts = this.ReadList(this.ReadComposablePartDefinition);

                var obj =  ComposableCatalog.Create(Resolver).AddParts(ComposablePartDefinition);

                return obj;
            }

            public void Serialize(ref MessagePackWriter writer, ComposableCatalog value, MessagePackSerializerOptions options)
            {

                options.Resolver.GetFormatterWithVerify<IImmutableSet<ComposablePartDefinition>>().Serialize(ref writer, value.Parts, options);
                options.Resolver.GetFormatterWithVerify<Resolver>().Serialize(ref writer, value.Resolver, options);
            }
        }


        /// <summary>
        /// The parts in the catalog.
        /// </summary>
        // [Key(3)]
        //[IgnoreMember]

        private ImmutableHashSet<ComposablePartDefinition> parts;

        /// <summary>
        /// The exports from parts in this catalog, indexed by contract name.
        /// </summary>
      //  [Key(4)]
        private ImmutableDictionary<string, ImmutableList<ExportDefinitionBinding>> exportsByContract;

        /// <summary>
        /// The types that are represented in this catalog.
        /// </summary>
      //  [Key(5)]
        private ImmutableHashSet<TypeRef> typesBackingParts;

        [SerializationConstructor]

        private ComposableCatalog(ImmutableHashSet<ComposablePartDefinition> parts, ImmutableDictionary<string, ImmutableList<ExportDefinitionBinding>> exportsByContract, ImmutableHashSet<TypeRef> typesBackingParts, DiscoveredParts discoveredParts, Resolver resolver)
        {
            Requires.NotNull(parts, nameof(parts));
            Requires.NotNull(exportsByContract, nameof(exportsByContract));
            Requires.NotNull(typesBackingParts, nameof(typesBackingParts));
            Requires.NotNull(discoveredParts, nameof(discoveredParts));
            Requires.NotNull(resolver, nameof(resolver));

            this.parts = parts;
            this.exportsByContract = exportsByContract;
            this.typesBackingParts = typesBackingParts;
            this.discoveredParts = discoveredParts;
            this.resolver = resolver;
        }

        /// <summary>
        /// Gets the set of parts that belong to the catalog.
        /// </summary>
      //  [Key(0)]
        public IImmutableSet<ComposablePartDefinition> Parts
        {
            get { return this.parts; }
        }

        /// <summary>
        /// Gets the parts that were added to the catalog via a <see cref="PartDiscovery"/> class.
        /// </summary>
      //  [Key(1)]
        public DiscoveredParts DiscoveredParts
        {
            get { return this.discoveredParts; }
            private set { this.discoveredParts = value; }
        }

       // [Key(2)]
        internal Resolver Resolver { get { return this.resolver; } }


      //  [Key(6)]

        private DiscoveredParts discoveredParts;


      //  [Key(7)]

        private Resolver resolver;

        public static ComposableCatalog Create(Resolver resolver)
        {
            return new ComposableCatalog(
                ImmutableHashSet.Create<ComposablePartDefinition>(),
                ImmutableDictionary.Create<string, ImmutableList<ExportDefinitionBinding>>(),
                ImmutableHashSet.Create<TypeRef>(),
                DiscoveredParts.Empty,
                resolver);
        }

        public ComposableCatalog AddPart(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, nameof(partDefinition));

            var parts = this.parts.Add(partDefinition);
            if (parts == this.parts)
            {
                // This part is already in the catalog.
                return this;
            }

            var typesBackingParts = this.typesBackingParts.Add(partDefinition.TypeRef);
            if (typesBackingParts == this.typesBackingParts)
            {
                Requires.Argument(false, nameof(partDefinition), Strings.TypeAlreadyInCatalogAsAnotherPart, partDefinition.TypeRef.FullName);
            }

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

            return new ComposableCatalog(parts, exportsByContract, typesBackingParts, this.DiscoveredParts, this.Resolver);
        }

        public ComposableCatalog AddParts(IEnumerable<ComposablePartDefinition> parts)
        {
            Requires.NotNull(parts, nameof(parts));

            // PERF: This has shown up on ETL traces as inefficient and expensive
            //       WithPart should call WithParts instead, and WithParts should
            //       execute a more efficient batch operation.
            return parts.Aggregate(this, (catalog, part) => catalog.AddPart(part));
        }

        public ComposableCatalog AddParts(DiscoveredParts parts)
        {
            Requires.NotNull(parts, nameof(parts));

            var catalog = this.AddParts(parts.Parts);
            return new ComposableCatalog(catalog.parts, catalog.exportsByContract, catalog.typesBackingParts, catalog.DiscoveredParts.Merge(parts), catalog.Resolver);
        }

        /// <summary>
        /// Merges this catalog with another one, including parts, discovery details and errors.
        /// </summary>
        /// <param name="catalogToMerge">The catalog to be merged with this one.</param>
        /// <returns>The merged version of the catalog.</returns>
        public ComposableCatalog AddCatalog(ComposableCatalog catalogToMerge)
        {
            Requires.NotNull(catalogToMerge, nameof(catalogToMerge));

            var catalog = this.AddParts(catalogToMerge.Parts);
            return new ComposableCatalog(catalog.parts, catalog.exportsByContract, catalog.typesBackingParts, catalog.DiscoveredParts.Merge(catalogToMerge.DiscoveredParts), catalog.Resolver);
        }

        /// <summary>
        /// Merges this catalog with others, including parts, discovery details and errors.
        /// </summary>
        /// <param name="catalogsToMerge">The catalogs to be merged with this one.</param>
        /// <returns>The merged version of the catalog.</returns>
        public ComposableCatalog AddCatalogs(IEnumerable<ComposableCatalog> catalogsToMerge)
        {
            Requires.NotNull(catalogsToMerge, nameof(catalogsToMerge));

            return catalogsToMerge.Aggregate(this, (aggregate, mergeCatalog) => aggregate.AddCatalog(mergeCatalog));
        }

        public IReadOnlyCollection<AssemblyName> GetInputAssemblies()
        {
            var assemblyCache = new Dictionary<Assembly, AssemblyName>();

            var inputAssemblies = ImmutableHashSet.CreateBuilder(ByValueEquality.AssemblyName);
            foreach (var part in this.Parts)
            {
                part.GetInputAssemblies(inputAssemblies, GetAssemblyName);
            }

            return inputAssemblies.ToImmutable();

            AssemblyName GetAssemblyName(Assembly assembly)
            {
                // Assembly.GetName() is non-trivial to calculate and adds up when asking
                // for the same set of assemblies over and over again, so cache the retrieval
                if (!assemblyCache.TryGetValue(assembly, out AssemblyName? assemblyName))
                {
                    assemblyName = assembly.GetName();
                    assemblyCache.Add(assembly, assemblyName);
                }

                return assemblyName;
            }
        }

        public bool Equals(ComposableCatalog? other)
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

        public IReadOnlyList<ExportDefinitionBinding> GetExports(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));

            // We always want to consider exports with a matching contract name.
            var exports = this.exportsByContract.GetValueOrDefault(importDefinition.ContractName, ImmutableList.Create<ExportDefinitionBinding>())!;

            // For those imports of generic types, we also want to consider exports that are based on open generic exports,
            string? genericTypeDefinitionContractName;
            Type[]? genericTypeArguments;
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

        internal static bool TryGetOpenGenericExport(ImportDefinition importDefinition, [NotNullWhen(true)] out string? contractName, [NotNullWhen(true)] out Type[]? typeArguments)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));

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
    }
}
