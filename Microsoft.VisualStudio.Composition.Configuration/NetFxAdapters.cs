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
    using MefV1 = System.ComponentModel.Composition;

    public static class NetFxAdapters
    {
        private static readonly ComposablePartDefinition compositionServicePart = (new AttributedPartDiscoveryV1()).CreatePart(typeof(CompositionService));
        private static readonly ComposablePartDefinition metadataViewImplProxyPart = (new AttributedPartDiscoveryV1()).CreatePart(typeof(MetadataViewImplProxy));
        private static readonly ComposablePartDefinition assemblyNameCodeBasePathPath = (new AttributedPartDiscoveryV1()).CreatePart(typeof(AssemblyLoadCodeBasePathLoader));

        /// <summary>
        /// Creates an instance of a <see cref="MefV1.Hosting.ExportProvider"/>
        /// for purposes of compatibility with the version of MEF found in the .NET Framework.
        /// </summary>
        /// <param name="exportProvider">The <see cref="Microsoft.VisualStudio.Composition.ExportProvider"/> to wrap.</param>
        /// <returns>A MEF "v1" shim.</returns>
        public static MefV1.Hosting.ExportProvider AsExportProvider(this ExportProvider exportProvider)
        {
            Requires.NotNull(exportProvider, "exportProvider");

            return new MefV1ExportProvider(exportProvider);
        }

        /// <summary>
        /// Creates a catalog that exports an instance of <see cref="MefV1.ICompositionService"/>.
        /// </summary>
        /// <param name="catalog">The catalog to add the export to.</param>
        /// <returns>A catalog that includes <see cref="MefV1.ICompositionService"/>.</returns>
        public static ComposableCatalog WithCompositionService(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            var modifiedCatalog = catalog.WithPart(compositionServicePart);
            return modifiedCatalog;
        }

        public static ComposableCatalog WithMetadataViewImplementationAttributeSupport(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            return catalog.WithPart(metadataViewImplProxyPart);
        }

        /// <summary>
        /// Adds the ability to load assemblies on demand based on their known location on disk when the
        /// catalog is initially created.
        /// </summary>
        /// <param name="catalog">The catalog to add the capability to.</param>
        /// <returns>The catalog with the added capability.</returns>
        public static ComposableCatalog WithAssemblyCodeBasePathLoading(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            return catalog.WithPart(assemblyNameCodeBasePathPath);
        }

        private class MefV1ExportProvider : MefV1.Hosting.ExportProvider
        {
            private readonly ExportProvider exportProvider;

            internal MefV1ExportProvider(ExportProvider exportProvider)
            {
                Requires.NotNull(exportProvider, "exportProvider");

                this.exportProvider = exportProvider;
            }

            protected override IEnumerable<MefV1.Primitives.Export> GetExportsCore(MefV1.Primitives.ImportDefinition definition, MefV1.Hosting.AtomicComposition atomicComposition)
            {
                var v3ImportDefinition = WrapImportDefinition(definition);
                var result = ImmutableList.CreateBuilder<MefV1.Primitives.Export>();
                var exports = this.exportProvider.GetExports(v3ImportDefinition);
                return exports.Select(e => new MefV1.Primitives.Export(e.Definition.ContractName, (IDictionary<string, object>)e.Metadata, () => e.Value));
            }

            private static ImportDefinition WrapImportDefinition(MefV1.Primitives.ImportDefinition definition)
            {
                Requires.NotNull(definition, "definition");
                var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty.Add(new ImportConstraint(definition));
                var cardinality = WrapCardinality(definition.Cardinality);
                return new ImportDefinition(definition.ContractName, cardinality, (IReadOnlyDictionary<string, object>)definition.Metadata, constraints);
            }

            private static ImportCardinality WrapCardinality(MefV1.Primitives.ImportCardinality cardinality)
            {
                switch (cardinality)
                {
                    case System.ComponentModel.Composition.Primitives.ImportCardinality.ExactlyOne:
                        return ImportCardinality.ExactlyOne;
                    case System.ComponentModel.Composition.Primitives.ImportCardinality.ZeroOrMore:
                        return ImportCardinality.ZeroOrMore;
                    case System.ComponentModel.Composition.Primitives.ImportCardinality.ZeroOrOne:
                        return ImportCardinality.OneOrZero;
                    default:
                        throw new ArgumentException();
                }
            }
        }

        private class ImportConstraint : IImportSatisfiabilityConstraint
        {
            private readonly MefV1.Primitives.ImportDefinition definition;

            internal ImportConstraint(MefV1.Primitives.ImportDefinition definition)
            {
                Requires.NotNull(definition, "definition");
                this.definition = definition;
            }

            public bool IsSatisfiedBy(ExportDefinition exportDefinition)
            {
                var v3ExportDefinition = new MefV1.Primitives.ExportDefinition(
                    exportDefinition.ContractName,
                    (IDictionary<string, object>)exportDefinition.Metadata);
                return this.definition.IsConstraintSatisfiedBy(v3ExportDefinition);
            }
        }

        // The part creation policy is NonShared so that it can satisfy exports within any sharing boundary.
        [MefV1.Export(typeof(MefV1.ICompositionService)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        private class CompositionService : MefV1.ICompositionService, IDisposable
        {
            private MefV1.Hosting.CompositionContainer container;

            [MefV1.ImportingConstructor]
            private CompositionService([MefV1.Import] ExportProvider exportProvider)
            {
                Requires.NotNull(exportProvider, "exportProvider");
                this.container = new MefV1.Hosting.CompositionContainer(exportProvider.AsExportProvider());
            }

            public void SatisfyImportsOnce(MefV1.Primitives.ComposablePart part)
            {
                this.container.SatisfyImportsOnce(part);
            }

            public void Dispose()
            {
                this.container.Dispose();
            }
        }

        [MefV1.Export(typeof(IMetadataViewProvider))]
        [MefV1.ExportMetadata("OrderPrecedence", 100)] // should take precedence over the transparent proxy
        private class MetadataViewImplProxy : IMetadataViewProvider
        {
            public bool IsDefaultMetadataRequired
            {
                get { return false; }
            }

            public bool IsMetadataViewSupported(Type metadataType)
            {
                return FindImplClassConstructor(metadataType) != null;
            }

            public TMetadata CreateProxy<TMetadata>(IReadOnlyDictionary<string, object> metadata)
            {
                var ctor = FindImplClassConstructor(typeof(TMetadata));
                return (TMetadata)ctor.Invoke(new object[] { metadata });
            }

            private static ConstructorInfo FindImplClassConstructor(Type metadataType)
            {
                Requires.NotNull(metadataType, "metadataType");
                var attr = (MefV1.MetadataViewImplementationAttribute)metadataType.GetCustomAttributes(typeof(MefV1.MetadataViewImplementationAttribute), false)
                    .FirstOrDefault();
                if (attr != null)
                {
                    if (metadataType.IsAssignableFrom(attr.ImplementationType))
                    {
                        var ctors = from ctor in attr.ImplementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                                    let parameters = ctor.GetParameters()
                                    where parameters.Length == 1 && (
                                        parameters[0].ParameterType.IsAssignableFrom(typeof(IDictionary<string, object>)) ||
                                        parameters[0].ParameterType.IsAssignableFrom(typeof(IReadOnlyDictionary<string, object>)))
                                    select ctor;
                        return ctors.FirstOrDefault();
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// An assembly loader that includes the code base path so we can load assemblies by path when necessary.
        /// </summary>
        [MefV1.Export(typeof(IAssemblyLoader))]
        [MefV1.ExportMetadata("OrderPrecedence", 100)] // should take precedence over one without codebase path handling
        private class AssemblyLoadCodeBasePathLoader : IAssemblyLoader
        {
            public Assembly LoadAssembly(string assemblyFullName, string codeBasePath)
            {
                Requires.NotNullOrEmpty(assemblyFullName, "assemblyFullName");

                var assemblyName = new AssemblyName(assemblyFullName);
                if (!string.IsNullOrEmpty(codeBasePath))
                {
                    assemblyName.CodeBase = codeBasePath;
                }

                return Assembly.Load(assemblyName);
            }
        }
    }
}
