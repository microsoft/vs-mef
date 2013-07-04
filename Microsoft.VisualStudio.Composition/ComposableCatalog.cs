namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

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
            return exports;
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

        public static ComposableCatalog Create(IEnumerable<Type> types, PartDiscovery discovery = null) {
            Requires.NotNull(types, "types");
            discovery = discovery ?? new AttributedPartDiscovery();

            return Create(types.Select(discovery.CreatePart));
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

            foreach (var export in partDefinition.ExportDefinitions)
            {
                var list = exportsByContract.GetValueOrDefault(export.Contract, ImmutableList.Create<Export>());
                exportsByContract = exportsByContract.SetItem(export.Contract, list.Add(new Export(export, partDefinition)));
            }

            return new ComposableCatalog(types, parts, exportsByContract);
        }
    }
}
