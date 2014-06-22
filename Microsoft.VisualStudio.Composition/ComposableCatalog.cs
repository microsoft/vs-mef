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

    public class ComposableCatalog
    {
        private ImmutableHashSet<Type> types;

        private ImmutableHashSet<ComposablePartDefinition> parts;

        private ImmutableDictionary<string, ImmutableList<ExportDefinitionBinding>> exportsByContract;

        private ComposableCatalog(ImmutableHashSet<Type> types, ImmutableHashSet<ComposablePartDefinition> parts, ImmutableDictionary<string, ImmutableList<ExportDefinitionBinding>> exportsByContract)
        {
            Requires.NotNull(types, "types");
            Requires.NotNull(parts, "parts");
            Requires.NotNull(exportsByContract, "exportsByContract");

            this.types = types;
            this.parts = parts;
            this.exportsByContract = exportsByContract;
        }

        public IReadOnlyList<ExportDefinitionBinding> GetExports(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            var exports = this.exportsByContract.GetValueOrDefault(importDefinition.ContractName, ImmutableList.Create<ExportDefinitionBinding>());

            var filteredExports = from export in exports
                                  where importDefinition.ExportContraints.All(c => c.IsSatisfiedBy(export.ExportDefinition))
                                  select export;

            return ImmutableList.CreateRange(filteredExports);
        }

        public IEnumerable<Assembly> Assemblies
        {
            get { return this.types.Select(t => t.GetTypeInfo().Assembly).Distinct(); }
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
                ImmutableDictionary.Create<string, ImmutableList<ExportDefinitionBinding>>());
        }

        public static ComposableCatalog Create(IEnumerable<ComposablePartDefinition> parts)
        {
            return parts.Aggregate(Create(), (catalog, part) => catalog.WithPart(part));
        }

        public static ComposableCatalog Create(PartDiscovery discovery, IEnumerable<Type> types)
        {
            Requires.NotNull(types, "types");
            Requires.NotNull(discovery, "discovery");

            return Create(types.Select(discovery.CreatePart).Where(p => p != null));
        }

        public static ComposableCatalog Create(PartDiscovery discovery, params Type[] types)
        {
            Requires.NotNull(types, "types");
            return Create(discovery, (IEnumerable<Type>)types);
        }

        public ComposableCatalog WithPart(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, "partDefinition");

            var types = this.types.Add(partDefinition.Type);
            var parts = this.parts.Add(partDefinition);
            var exportsByContract = this.exportsByContract;

            foreach (var export in partDefinition.ExportedTypes)
            {
                var list = exportsByContract.GetValueOrDefault(export.Contract.ContractName, ImmutableList.Create<ExportDefinitionBinding>());
                exportsByContract = exportsByContract.SetItem(export.Contract.ContractName, list.Add(new ExportDefinitionBinding(export, partDefinition, exportingMember: null)));
            }

            foreach (var exportPair in partDefinition.ExportingMembers)
            {
                var member = exportPair.Key;
                foreach (var export in exportPair.Value)
                {
                    var list = exportsByContract.GetValueOrDefault(export.Contract.ContractName, ImmutableList.Create<ExportDefinitionBinding>());
                    exportsByContract = exportsByContract.SetItem(export.Contract.ContractName, list.Add(new ExportDefinitionBinding(export, partDefinition, member)));
                }
            }

            return new ComposableCatalog(types, parts, exportsByContract);
        }
    }
}
