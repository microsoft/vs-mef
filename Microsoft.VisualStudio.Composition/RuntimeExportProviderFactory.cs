namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal class RuntimeExportProviderFactory : IExportProviderFactory
    {
        private readonly CompositionConfiguration configuration;
        private readonly IReadOnlyDictionary<ComposablePartDefinition, ComposedPart> partDefinitionToComposedPart;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<ExportDefinitionBinding>> exportsByContract;

        internal RuntimeExportProviderFactory(CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, "configuration");
            this.configuration = configuration;

            var exports =
                from part in this.configuration.Parts
                from exportingMemberAndDefinition in part.Definition.ExportDefinitions
                let export = new ExportDefinitionBinding(exportingMemberAndDefinition.Value, part.Definition, exportingMemberAndDefinition.Key)
                where part.Definition.IsInstantiable
                group export by export.ExportDefinition.ContractName into exportsByContract
                select exportsByContract;
            this.exportsByContract = exports.ToDictionary<IGrouping<string, ExportDefinitionBinding>, string, IReadOnlyList<ExportDefinitionBinding>>(
                e => e.Key, e => e.ToList());

            this.partDefinitionToComposedPart = this.configuration.Parts.ToDictionary(p => p.Definition);
        }

        public ExportProvider CreateExportProvider()
        {
            return new RuntimeExportProvider(this);
        }

        private class RuntimeExportProvider : ExportProvider
        {
            private readonly RuntimeExportProviderFactory factory;

            internal RuntimeExportProvider(RuntimeExportProviderFactory factory)
                : this(factory, null, null)
            {
            }

            internal RuntimeExportProvider(RuntimeExportProviderFactory factory, ExportProvider parent, string[] freshSharingBoundaries)
                : base(parent, freshSharingBoundaries)
            {
                Requires.NotNull(factory, "factory");
                this.factory = factory;
                this.cachedTypes = new Type[0];
            }

            protected override int GetTypeIdCore(Type type)
            {
                return -1;
            }

            protected override IEnumerable<Export> GetExportsCore(ImportDefinition importDefinition)
            {
                IReadOnlyList<ExportDefinitionBinding> exports;
                if (!this.factory.exportsByContract.TryGetValue(importDefinition.ContractName, out exports))
                {
                    return Enumerable.Empty<Export>();
                }

                return exports.Select(export =>
                    this.CreateExport(
                        importDefinition,
                        export.ExportDefinition.Metadata,
                        this.GetTypeId(export.PartDefinition.Type),
                        (ep, provisionalSharedObjects) => this.CreatePart(ep, provisionalSharedObjects, export),
                        export.PartDefinition.IsShared ? this.factory.configuration.GetEffectiveSharingBoundary(export.PartDefinition) : null,
                        !export.PartDefinition.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinition),
                        export.ExportingMember));
            }

            private object CreatePart(ExportProvider exportProvider, Dictionary<int, object> provisionalSharedObjects, ExportDefinitionBinding exportDefinition)
            {
                var composedPart = this.factory.partDefinitionToComposedPart[exportDefinition.PartDefinition];
                object part = exportDefinition.PartDefinition.ImportingConstructorInfo.Invoke(EmptyObjectArray);

                foreach (var importExports in composedPart.SatisfyingExports)
                {
                    var import = importExports.Key;
                    var exports = importExports.Value;
                    var export = exports.Single();
                    var exportedValue = this.GetOrCreateShareableValue(
                        this.GetTypeId(export.PartDefinition.Type),
                        (ep, pso) => this.CreatePart(ep, pso, export),
                        provisionalSharedObjects,
                        export.PartDefinition.IsShared ? this.factory.configuration.GetEffectiveSharingBoundary(export.PartDefinition) : null,
                        !export.PartDefinition.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(import.ImportDefinition));
                    SetImportingMember(part, import.ImportingMember, exportedValue.Value);
                }

                return part;
            }

            private static void SetImportingMember(object part, MemberInfo member, object value)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    property.SetValue(part, value);
                    return;
                }

                var field = member as FieldInfo;
                if (field != null)
                {
                    field.SetValue(part, value);
                    return;
                }

                throw new NotSupportedException();
            }
        }
    }
}
