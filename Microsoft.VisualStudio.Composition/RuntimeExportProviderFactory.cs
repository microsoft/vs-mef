namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
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
                var partDefinition = exportDefinition.PartDefinition;
                var composedPart = this.factory.partDefinitionToComposedPart[partDefinition];
                var ctorArgs = composedPart.GetImportingConstructorImports()
                    .Select(pair => GetValueForImportSite(pair.Key, pair.Value, provisionalSharedObjects)).ToArray();
                object part = exportDefinition.PartDefinition.ImportingConstructorInfo.Invoke(ctorArgs);

                foreach (var importExports in composedPart.SatisfyingExports)
                {
                    var import = importExports.Key;
                    var exports = importExports.Value;
                    if (import.ImportingMember != null)
                    {
                        SetImportingMember(part, import.ImportingMember, this.GetValueForImportSite(import, exports, provisionalSharedObjects));
                    }
                }

                if (partDefinition.OnImportsSatisfied != null)
                {
                    partDefinition.OnImportsSatisfied.Invoke(part, EmptyObjectArray);
                }

                return part;
            }

            private object GetValueForImportSite(ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports, Dictionary<int, object> provisionalSharedObjects)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(exports, "exports");
                Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

                if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    var export = exports.FirstOrDefault();
                    if (export == null)
                    {
                        return null;
                    }

                    ILazy<object> exportedValue = this.GetExportedValue(import, export, provisionalSharedObjects);

                    object importedValue = import.IsLazy
                        ? CreateStrongTypedLazy(exportedValue.ValueFactory, export.ExportDefinition.Metadata, import.ImportingSiteTypeWithoutCollection)
                        : exportedValue.Value;
                    return importedValue;
                }
            }

            private static object CreateStrongTypedLazy(Func<object> valueFactory, IReadOnlyDictionary<string, object> metadata, Type lazyType)
            {
                Requires.NotNull(valueFactory, "valueFactory");
                Requires.NotNull(metadata, "metadata");

                using (var ctorArgs = GetObjectArray(lazyType.GenericTypeArguments.Length))
                {
                    ctorArgs.Value[0] = ReflectionHelpers.CreateFuncOfType(lazyType.GenericTypeArguments[0], valueFactory);
                    if (ctorArgs.Value.Length == 2)
                    {
                        ctorArgs.Value[1] = GetStrongTypedMetadata(metadata, lazyType.GenericTypeArguments[1]);
                    }

                    object lazyInstance = Activator.CreateInstance(lazyType, ctorArgs.Value);
                    return lazyInstance;
                }
            }

            private static object GetStrongTypedMetadata(IReadOnlyDictionary<string, object> metadata, Type lazyType)
            {
                // TODO: get the metadata type right.
                return metadata;
            }

            private ILazy<object> GetExportedValue(ImportDefinitionBinding import, ExportDefinitionBinding export, Dictionary<int, object> provisionalSharedObjects)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(export, "export");
                Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

                var exportedValue = this.GetOrCreateShareableValue(
                    this.GetTypeId(export.PartDefinition.Type),
                    (ep, pso) => this.CreatePart(ep, pso, export),
                    provisionalSharedObjects,
                    export.PartDefinition.IsShared ? this.factory.configuration.GetEffectiveSharingBoundary(export.PartDefinition) : null,
                    !export.PartDefinition.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(import.ImportDefinition));
                return exportedValue;
            }

            private static void SetImportingMember(object part, MemberInfo member, object value)
            {
                Requires.NotNull(part, "part");
                Requires.NotNull(member, "member");

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

            private struct Rental<T> : IDisposable
                where T : class
            {
                private T value;
                private Stack<T> returnTo;
                private Action<T> cleanup;

                internal Rental(Stack<T> returnTo, Func<T> create, Action<T> cleanup)
                {
                    this.value = returnTo != null && returnTo.Count > 0 ? returnTo.Pop() : create();
                    this.returnTo = returnTo;
                    this.cleanup = cleanup;
                }

                public T Value
                {
                    get { return this.value; }
                }

                public void Dispose()
                {
                    Assumes.NotNull(this.value);

                    var value = this.value;
                    this.value = null;
                    if (this.cleanup != null)
                    {
                        this.cleanup(value);
                    }

                    if (this.returnTo != null)
                    {
                        this.returnTo.Push(value);
                    }
                }
            }

            private static readonly ThreadLocal<Stack<object[]>> OneElementObjectArray = new ThreadLocal<Stack<object[]>>(() => new Stack<object[]>());
            private static readonly ThreadLocal<Stack<object[]>> TwoElementObjectArray = new ThreadLocal<Stack<object[]>>(() => new Stack<object[]>());

            private static Rental<object[]> GetObjectArray(int length)
            {
                switch (length)
                {
                    case 1:
                        return new Rental<object[]>(OneElementObjectArray.Value, () => new object[1], v => Array.Clear(v, 0, v.Length));
                    case 2:
                        return new Rental<object[]>(TwoElementObjectArray.Value, () => new object[2], v => Array.Clear(v, 0, v.Length));
                    default:
                        return new Rental<object[]>(null, () => new object[length], null);
                }
            }
        }
    }
}
