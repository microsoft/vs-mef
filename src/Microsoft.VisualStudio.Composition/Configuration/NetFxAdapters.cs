// Copyright (c) Microsoft. All rights reserved.

#if NET45

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.Composition.ReflectionModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Reflection;
    using MefV1 = System.ComponentModel.Composition;

    public static class NetFxAdapters
    {
        private static readonly ComposablePartDefinition CompositionServicePart;

        static NetFxAdapters()
        {
            var discovery = new AttributedPartDiscoveryV1(Resolver.DefaultInstance);
            CompositionServicePart = discovery.CreatePart(typeof(CompositionService));
        }

        /// <summary>
        /// Creates an instance of a <see cref="MefV1.Hosting.ExportProvider"/>
        /// for purposes of compatibility with the version of MEF found in the .NET Framework.
        /// </summary>
        /// <param name="exportProvider">The <see cref="Microsoft.VisualStudio.Composition.ExportProvider"/> to wrap.</param>
        /// <returns>A MEF "v1" shim.</returns>
        public static MefV1.Hosting.ExportProvider AsExportProvider(this ExportProvider exportProvider)
        {
            Requires.NotNull(exportProvider, nameof(exportProvider));

            return new MefV1ExportProvider(exportProvider);
        }

        /// <summary>
        /// Creates a catalog that exports an instance of <see cref="MefV1.ICompositionService"/>.
        /// </summary>
        /// <param name="catalog">The catalog to add the export to.</param>
        /// <returns>A catalog that includes <see cref="MefV1.ICompositionService"/>.</returns>
        public static ComposableCatalog WithCompositionService(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, nameof(catalog));

            var modifiedCatalog = catalog.AddPart(CompositionServicePart);
            return modifiedCatalog;
        }

        /// <summary>
        /// Adds parts that allow MEF to work on .NET Framework platforms.
        /// </summary>
        /// <param name="catalog">The catalog to add desktop support to.</param>
        /// <returns>The catalog that includes desktop support.</returns>
        [Obsolete("Desktop support is automatically included when run on the .NET Framework.")]
        public static ComposableCatalog WithDesktopSupport(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, nameof(catalog));
            return catalog;
        }

        private class MefV1ExportProvider : MefV1.Hosting.ExportProvider
        {
            private static readonly Type ExportFactoryV1Type = typeof(MefV1.ExportFactory<object, IDictionary<string, object>>);
#pragma warning disable SA1310 // Field names must not contain underscore
            private static readonly Type IPartCreatorImportDefinition_MightFail = typeof(MefV1.Primitives.ImportDefinition).Assembly.GetType("System.ComponentModel.Composition.Primitives.IPartCreatorImportDefinition", throwOnError: false);
            private static readonly PropertyInfo ProductImportDefinition_MightFail = IPartCreatorImportDefinition_MightFail != null ? IPartCreatorImportDefinition_MightFail.GetProperty("ProductImportDefinition", BindingFlags.Instance | BindingFlags.Public) : null;
#pragma warning restore SA1310 // Field names must not contain underscore
            private static readonly string ExportFactoryV1TypeIdentity = PartDiscovery.GetContractName(ExportFactoryV1Type);

            private readonly ExportProvider exportProvider;

            internal MefV1ExportProvider(ExportProvider exportProvider)
            {
                Requires.NotNull(exportProvider, nameof(exportProvider));

                this.exportProvider = exportProvider;
            }

            protected override IEnumerable<MefV1.Primitives.Export> GetExportsCore(MefV1.Primitives.ImportDefinition definition, MefV1.Hosting.AtomicComposition atomicComposition)
            {
                var v3ImportDefinition = WrapImportDefinition(definition);
                var result = ImmutableList.CreateBuilder<MefV1.Primitives.Export>();
                var exports = this.exportProvider.GetExports(v3ImportDefinition);
                return exports.Select(UnwrapExport).ToArray();
            }

            private static ImportDefinition WrapImportDefinition(MefV1.Primitives.ImportDefinition definition)
            {
                Requires.NotNull(definition, nameof(definition));

                var contractImportDefinition = definition as MefV1.Primitives.ContractBasedImportDefinition;

                var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty
                    .Add(new ImportConstraint(definition));
                if (contractImportDefinition != null)
                {
                    constraints = constraints.Union(PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraints(WrapCreationPolicy(contractImportDefinition.RequiredCreationPolicy)));
                }

                // Do NOT propagate the cardinality otherwise the export provider will throw
                // if the cardinality is not met. But it's not our job to throw, since the caller
                // is going to aggregate our response with other export providers and finally
                // be responsible for throwing if the ultimate receiver of the result doesn't
                // get what they expect.
                // We use ZeroOrMore to indicate we'll accept any response.
                var cardinality = ImportCardinality.ZeroOrMore;

                var metadata = (IReadOnlyDictionary<string, object>)definition.Metadata;

                MefV1.Primitives.ImportDefinition productImportDefinitionV1 = GetExportFactoryProductImportDefinitionIfApplicable(definition);

                if (productImportDefinitionV1 != null)
                {
                    var productImportDefinitionV3 = WrapImportDefinition(productImportDefinitionV1);
                    metadata = metadata.ToImmutableDictionary()
                        .Add(CompositionConstants.ExportFactoryProductImportDefinition, productImportDefinitionV3)
                        .Add(CompositionConstants.ExportFactoryTypeMetadataName, ExportFactoryV1Type);
                }

                return new ImportDefinition(definition.ContractName, cardinality, metadata, constraints);
            }

            /// <summary>
            /// Extracts the ImportDefinition for the T in an ExportFactory{T} import, if applicable.
            /// </summary>
            /// <param name="definition">The ImportDefinition which may be an ExportFactory.</param>
            /// <returns>The import definition that describes the created part, or <c>null</c> if the import definition isn't an ExportFactory.</returns>
            private static MefV1.Primitives.ImportDefinition GetExportFactoryProductImportDefinitionIfApplicable(MefV1.Primitives.ImportDefinition definition)
            {
                // The optimal path that we can code for at the moment is using the internal interface.
                if (IPartCreatorImportDefinition_MightFail != null && ProductImportDefinition_MightFail != null)
                {
                    if (IPartCreatorImportDefinition_MightFail.IsInstanceOfType(definition))
                    {
                        return (MefV1.Primitives.ImportDefinition)ProductImportDefinition_MightFail.GetValue(definition);
                    }
                }
                else
                {
                    // The internal interface, or its member, is gone. Fallback to using the public API that throws.
                    try
                    {
                        if (ReflectionModelServices.IsExportFactoryImportDefinition(definition))
                        {
                            return ReflectionModelServices.GetExportFactoryProductImportDefinition(definition);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // MEFv1 throws rather than simply returning false when the ImportDefinition is of the incorrect type.
                    }
                }

                return null;
            }

            private static IDictionary<string, object> GetMefV1ExportDefinitionMetadataFromV3(IReadOnlyDictionary<string, object> exportDefinitionMetadata)
            {
                Requires.NotNull(exportDefinitionMetadata, nameof(exportDefinitionMetadata));
                var metadata = exportDefinitionMetadata.ToImmutableDictionary();

                CreationPolicy creationPolicy;
                if (metadata.TryGetValue(CompositionConstants.PartCreationPolicyMetadataName, out creationPolicy))
                {
                    metadata = metadata.SetItem(
                        CompositionConstants.PartCreationPolicyMetadataName,
                        UnwrapCreationPolicy(creationPolicy));
                }

                ExportDefinition productDefinitionMetadatum;
                if (metadata.TryGetValue(CompositionConstants.ProductDefinitionMetadataName, out productDefinitionMetadatum))
                {
                    // The value of this metadata is expected to be a V3 ExportDefinition. We must adapt it to be a V1 ExportDefinition
                    // so that MEFv1 can deal with it.
                    metadata = metadata.SetItem(
                        CompositionConstants.ProductDefinitionMetadataName,
                        new MefV1.Primitives.ExportDefinition(
                            productDefinitionMetadatum.ContractName,
                            GetMefV1ExportDefinitionMetadataFromV3(productDefinitionMetadatum.Metadata)));
                }

                return metadata;
            }

            private static MefV1.CreationPolicy UnwrapCreationPolicy(CreationPolicy creationPolicy)
            {
                switch (creationPolicy)
                {
                    case CreationPolicy.Any:
                        return MefV1.CreationPolicy.Any;
                    case CreationPolicy.Shared:
                        return MefV1.CreationPolicy.Shared;
                    case CreationPolicy.NonShared:
                        return MefV1.CreationPolicy.NonShared;
                    default:
                        throw new ArgumentException();
                }
            }

            private static CreationPolicy WrapCreationPolicy(MefV1.CreationPolicy creationPolicy)
            {
                switch (creationPolicy)
                {
                    case MefV1.CreationPolicy.Any:
                        return CreationPolicy.Any;
                    case MefV1.CreationPolicy.Shared:
                        return CreationPolicy.Shared;
                    case MefV1.CreationPolicy.NonShared:
                        return CreationPolicy.NonShared;
                    default:
                        throw new ArgumentException();
                }
            }

            private static MefV1.Primitives.Export UnwrapExport(Export export)
            {
                var metadata = GetMefV1ExportDefinitionMetadataFromV3(export.Metadata);

                if (export.Definition.ContractName == ExportFactoryV1TypeIdentity)
                {
                    return new MefV1.Primitives.Export(
                        "System.ComponentModel.Composition.Contracts.ExportFactory",
                        metadata,
                        () => new ComposablePartDefinitionForExportFactory((MefV1.ExportFactory<object, IDictionary<string, object>>)export.Value));
                }

                return new MefV1.Primitives.Export(
                    export.Definition.ContractName,
                    metadata,
                    () => UnwrapExportedValue(export.Value));
            }

            private static object UnwrapExportedValue(object value)
            {
                if (value is ExportedDelegate)
                {
                    var del = ((ExportedDelegate)value).CreateDelegate(typeof(Delegate));
                    return new MefV1.Primitives.ExportedDelegate(del.Target, del.Method);
                }
                else
                {
                    return value;
                }
            }

            private class ComposablePartForExportFactory : MefV1.Primitives.ComposablePart, IDisposable
            {
                internal static readonly MefV1.Primitives.ExportDefinition ExportFactoryDefinitionSentinel = new MefV1.Primitives.ExportDefinition("ExportFactoryValue", ImmutableDictionary<string, object>.Empty);

                private readonly MefV1.ExportLifetimeContext<object> value;

                internal ComposablePartForExportFactory(MefV1.ExportLifetimeContext<object> value)
                {
                    this.value = value;
                }

                public override IEnumerable<MefV1.Primitives.ExportDefinition> ExportDefinitions
                {
                    get { throw new NotImplementedException(); }
                }

                public override IEnumerable<MefV1.Primitives.ImportDefinition> ImportDefinitions
                {
                    get { throw new NotImplementedException(); }
                }

                public override object GetExportedValue(MefV1.Primitives.ExportDefinition definition)
                {
                    if (definition == ExportFactoryDefinitionSentinel)
                    {
                        return this.value.Value;
                    }

                    throw new NotImplementedException();
                }

                public override void SetImport(MefV1.Primitives.ImportDefinition definition, IEnumerable<MefV1.Primitives.Export> exports)
                {
                    throw new NotImplementedException();
                }

                public void Dispose()
                {
                    this.value.Dispose();
                }
            }

            private class ComposablePartDefinitionForExportFactory : MefV1.Primitives.ComposablePartDefinition
            {
                private static readonly MefV1.Primitives.ExportDefinition[] SentinelExportDefinitionArray = new[] { ComposablePartForExportFactory.ExportFactoryDefinitionSentinel };
                private readonly MefV1.ExportFactory<object, IDictionary<string, object>> exportFactory;

                internal ComposablePartDefinitionForExportFactory(MefV1.ExportFactory<object, IDictionary<string, object>> exportFactory)
                {
                    Requires.NotNull(exportFactory, nameof(exportFactory));
                    this.exportFactory = exportFactory;
                }

                public override IEnumerable<MefV1.Primitives.ExportDefinition> ExportDefinitions
                {
                    get { return SentinelExportDefinitionArray; }
                }

                public override IEnumerable<MefV1.Primitives.ImportDefinition> ImportDefinitions
                {
                    get { return Enumerable.Empty<MefV1.Primitives.ImportDefinition>(); }
                }

                public override MefV1.Primitives.ComposablePart CreatePart()
                {
                    MefV1.ExportLifetimeContext<object> value = this.exportFactory.CreateExport();
                    return new ComposablePartForExportFactory(value);
                }
            }
        }

        private class ImportConstraint : IImportSatisfiabilityConstraint
        {
            private readonly MefV1.Primitives.ImportDefinition definition;

            internal ImportConstraint(MefV1.Primitives.ImportDefinition definition)
            {
                Requires.NotNull(definition, nameof(definition));
                this.definition = definition;
            }

            public bool IsSatisfiedBy(ExportDefinition exportDefinition)
            {
                var v1ExportDefinition = new MefV1.Primitives.ExportDefinition(
                    exportDefinition.ContractName,
                    (IDictionary<string, object>)exportDefinition.Metadata);
                return this.definition.IsConstraintSatisfiedBy(v1ExportDefinition);
            }

            public bool Equals(IImportSatisfiabilityConstraint obj)
            {
                var other = obj as ImportConstraint;
                if (other == null)
                {
                    return false;
                }

                return this.definition.Equals(other.definition);
            }
        }

#pragma warning disable CS3016 // Arrays as attribute arguments is not CLS compliant. But these are private so we don't care.

        // The part creation policy is NonShared so that it can satisfy exports within any sharing boundary.
        [MefV1.Export(typeof(MefV1.ICompositionService))]
        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.PartMetadata(CompositionConstants.DgmlCategoryPartMetadataName, new string[] { "VsMEFBuiltIn" })]
        private class CompositionService : MefV1.ICompositionService, IDisposable
        {
            private MefV1.Hosting.CompositionContainer container;

            [MefV1.ImportingConstructor]
            private CompositionService([MefV1.Import] ExportProvider exportProvider)
            {
                Requires.NotNull(exportProvider, nameof(exportProvider));
                this.container = new MefV1.Hosting.CompositionContainer(MefV1.Hosting.CompositionOptions.IsThreadSafe, exportProvider.AsExportProvider());
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
    }
}

#endif
