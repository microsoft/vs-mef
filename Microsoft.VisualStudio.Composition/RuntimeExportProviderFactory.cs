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
                    .Select(pair => GetValueForImportSite(null, pair.Key, pair.Value, provisionalSharedObjects).Value).ToArray();
                object part = exportDefinition.PartDefinition.ImportingConstructorInfo.Invoke(ctorArgs);

                foreach (var importExports in composedPart.SatisfyingExports)
                {
                    var import = importExports.Key;
                    var exports = importExports.Value;
                    if (import.ImportingMember != null)
                    {
                        var value = this.GetValueForImportSite(part, import, exports, provisionalSharedObjects);
                        if (value.ValueShouldBeSet)
                        {
                            SetImportingMember(part, import.ImportingMember, value);
                        }
                    }
                }

                if (partDefinition.OnImportsSatisfied != null)
                {
                    partDefinition.OnImportsSatisfied.Invoke(part, EmptyObjectArray);
                }

                return part;
            }

            private struct ValueForImportSite
            {
                internal ValueForImportSite(object value)
                    : this()
                {
                    this.Value = value;
                    this.ValueShouldBeSet = true;
                }

                public bool ValueShouldBeSet { get; private set; }

                public object Value { get; private set; }
            }

            private ValueForImportSite GetValueForImportSite(object part, ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports, Dictionary<int, object> provisionalSharedObjects)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(exports, "exports");
                Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

                if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
                {
                    if (import.ImportingSiteType.IsArray || (import.ImportingSiteType.GetTypeInfo().IsGenericType && import.ImportingSiteType.GetGenericTypeDefinition().IsEquivalentTo(typeof(IEnumerable<>))))
                    {
                        Array array = Array.CreateInstance(import.ImportingSiteTypeWithoutCollection, exports.Count);
                        using (var intArray = ArrayRental<int>.Get(1))
                        {
                            for (int i = 0; i < exports.Count; i++)
                            {
                                intArray.Value[0] = i;
                                array.SetValue(this.GetValueForImportElement(import, exports[i], provisionalSharedObjects), intArray.Value);
                            }
                        }

                        return new ValueForImportSite(array);
                    }
                    else
                    {
                        object collectionObject = null;
                        if (import.ImportingMember != null)
                        {
                            collectionObject = GetImportingMember(part, import.ImportingMember);
                        }

                        if (collectionObject == null)
                        {
                            if (PartDiscovery.IsImportManyCollectionTypeCreateable(import))
                            {
                                using (var typeArgs = ArrayRental<Type>.Get(1))
                                {
                                    typeArgs.Value[0] = import.ImportingSiteTypeWithoutCollection;
                                    Type listType = typeof(List<>).MakeGenericType(typeArgs.Value);
                                    if (import.ImportingSiteType.GetTypeInfo().IsAssignableFrom(listType.GetTypeInfo()))
                                    {
                                        collectionObject = Activator.CreateInstance(listType);
                                    }
                                    else
                                    {
                                        collectionObject = Activator.CreateInstance(import.ImportingSiteType);
                                    }
                                }

                                SetImportingMember(part, import.ImportingMember, collectionObject);
                            }
                            else
                            {
                                throw new CompositionFailedException("Unable to instantiate custom import collection type.");
                            }
                        }

                        var collectionAccessor = new CollectionWrapper(collectionObject, import.ImportingSiteTypeWithoutCollection);
                        for (int i = 0; i < exports.Count; i++)
                        {
                            collectionAccessor.Add(this.GetValueForImportElement(import, exports[i], provisionalSharedObjects));
                        }

                        return new ValueForImportSite(); // signal caller should not set value again.
                    }
                }
                else
                {
                    var export = exports.FirstOrDefault();
                    if (export == null)
                    {
                        return new ValueForImportSite(null);
                    }

                    return new ValueForImportSite(this.GetValueForImportElement(import, export, provisionalSharedObjects));
                }
            }

            private struct CollectionWrapper
            {
                private readonly object collectionOfT;
                private readonly MethodInfo addMethod;
                private readonly MethodInfo clearMethod;

                internal CollectionWrapper(object collectionOfT, Type elementType)
                {
                    Requires.NotNull(collectionOfT, "collectionOfT");
                    this.collectionOfT = collectionOfT;
                    Type collectionType;
                    using (var args = ArrayRental<Type>.Get(1))
                    {
                        args.Value[0] = elementType;
                        collectionType = typeof(ICollection<>).MakeGenericType(args.Value);
                        this.addMethod = collectionType.GetRuntimeMethod("Add", args.Value);
                    }

                    using (var args = ArrayRental<Type>.Get(0))
                    {
                        this.clearMethod = collectionType.GetRuntimeMethod("Clear", args.Value);
                    }
                }

                internal void Add(object item)
                {
                    using (var args = ArrayRental<object>.Get(1))
                    {
                        args.Value[0] = item;
                        this.addMethod.Invoke(this.collectionOfT, args.Value);
                    }
                }

                internal void Clear()
                {
                    this.clearMethod.Invoke(this.collectionOfT, EmptyObjectArray);
                }
            }

            private object GetValueForImportElement(ImportDefinitionBinding import, ExportDefinitionBinding export, Dictionary<int, object> provisionalSharedObjects)
            {
                ILazy<object> exportedValue = this.GetExportedValue(import, export, provisionalSharedObjects);

                object importedValue = import.IsLazy
                    ? CreateStrongTypedLazy(exportedValue.ValueFactory, export.ExportDefinition.Metadata, import.ImportingSiteTypeWithoutCollection)
                    : exportedValue.Value;
                return importedValue;
            }

            private static object CreateStrongTypedLazy(Func<object> valueFactory, IReadOnlyDictionary<string, object> metadata, Type lazyType)
            {
                Requires.NotNull(valueFactory, "valueFactory");
                Requires.NotNull(metadata, "metadata");

                using (var ctorArgs = ArrayRental<object>.Get(lazyType.GenericTypeArguments.Length))
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

            private static object GetImportingMember(object part, MemberInfo member)
            {
                Requires.NotNull(part, "part");
                Requires.NotNull(member, "member");

                var property = member as PropertyInfo;
                if (property != null)
                {
                    return property.GetValue(part);
                }

                var field = member as FieldInfo;
                if (field != null)
                {
                    return field.GetValue(part);
                }

                throw new NotSupportedException();
            }

            private struct Rental<T> : IDisposable
                where T : class
            {
                private T value;
                private Stack<T> returnTo;
                private Action<T> cleanup;

                internal Rental(Stack<T> returnTo, Func<int, T> create, Action<T> cleanup, int createArg)
                {
                    this.value = returnTo != null && returnTo.Count > 0 ? returnTo.Pop() : create(createArg);
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

            private static class ArrayRental<T>
            {
                private static readonly ThreadLocal<Dictionary<int, Stack<T[]>>> arrays = new ThreadLocal<Dictionary<int, Stack<T[]>>>(() => new Dictionary<int, Stack<T[]>>());

                internal static Rental<T[]> Get(int length)
                {
                    Stack<T[]> stack;
                    if (!arrays.Value.TryGetValue(length, out stack))
                    {
                        arrays.Value.Add(length, stack = new Stack<T[]>());
                    }

                    return new Rental<T[]>(stack, len => new T[len], array => Array.Clear(array, 0, array.Length), length);
                }
            }
        }
    }
}
