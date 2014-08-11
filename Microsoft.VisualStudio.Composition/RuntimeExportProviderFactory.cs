namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;

    internal class RuntimeExportProviderFactory : IExportProviderFactory
    {
        private readonly RuntimeComposition composition;

        internal RuntimeExportProviderFactory(RuntimeComposition composition)
        {
            Requires.NotNull(composition, "composition");
            this.composition = composition;
        }

        public ExportProvider CreateExportProvider()
        {
            return new RuntimeExportProvider(this.composition);
        }

        private class RuntimeExportProvider : ExportProvider
        {
            private readonly RuntimeComposition composition;

            internal RuntimeExportProvider(RuntimeComposition composition)
                : this(composition, null, null)
            {
            }

            internal RuntimeExportProvider(RuntimeComposition composition, ExportProvider parent, IReadOnlyCollection<string> freshSharingBoundaries)
                : base(parent, freshSharingBoundaries)
            {
                Requires.NotNull(composition, "composition");

                this.composition = composition;
                this.cachedTypes = new Type[0];
            }

            protected override int GetTypeIdCore(Reflection.TypeRef type)
            {
                return -1;
            }

            protected override IEnumerable<ExportInfo> GetExportsCore(ImportDefinition importDefinition)
            {
                var exports = this.composition.GetExports(importDefinition.ContractName);

                return
                    from export in exports
                    let part = this.composition.GetPart(export)
                    select this.CreateExport(
                        importDefinition,
                        export.Metadata,
                        this.GetTypeId(GetPartConstructedTypeRef(part, importDefinition.Metadata)),
                        (ep, provisionalSharedObjects) => this.CreatePart(provisionalSharedObjects, part, importDefinition.Metadata),
                        part.SharingBoundary,
                        !part.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinition),
                        export.Member.Resolve());
            }

            private object CreatePart(Dictionary<int, object> provisionalSharedObjects, RuntimeComposition.RuntimePart partDefinition, IReadOnlyDictionary<string, object> importMetadata)
            {
                if (partDefinition.Type.Equals(Reflection.TypeRef.Get(ExportProvider.ExportProviderPartDefinition.Type)))
                {
                    // Special case for our synthesized part that acts as a placeholder for *this* export provider.
                    return this.NonDisposableWrapper.Value;
                }

                if (!partDefinition.IsInstantiable)
                {
                    throw new CompositionFailedException("Cannot instantiate this part.");
                }

                var constructedPartType = GetPartConstructedTypeRef(partDefinition, importMetadata);
                var ctorArgs = partDefinition.ImportingConstructorArguments
                    .Select(import => GetValueForImportSite(null, import, provisionalSharedObjects).Value).ToArray();
                ConstructorInfo importingConstructor = partDefinition.ImportingConstructor.Resolve();
                if (importingConstructor.ContainsGenericParameters)
                {
                    // TODO: fix this to find the precise match, including cases where the matching constructor includes a generic type parameter.
                    importingConstructor = constructedPartType.Resolve().GetTypeInfo().DeclaredConstructors.First(ctor => true);
                }

                object part = importingConstructor.Invoke(ctorArgs);

                if (partDefinition.IsShared)
                {
                    provisionalSharedObjects.Add(this.GetTypeId(constructedPartType), part);
                }

                var disposablePart = part as IDisposable;
                if (disposablePart != null)
                {
                    this.TrackDisposableValue(disposablePart, partDefinition.SharingBoundary);
                }

                foreach (var import in partDefinition.ImportingMembers)
                {
                    var value = this.GetValueForImportSite(part, import, provisionalSharedObjects);
                    if (value.ValueShouldBeSet)
                    {
                        this.SetImportingMember(part, import.ImportingMemberRef.Resolve(), value.Value);
                    }
                }

                if (!partDefinition.OnImportsSatisfied.IsEmpty)
                {
                    partDefinition.OnImportsSatisfied.Resolve().Invoke(part, EmptyObjectArray);
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

            private ValueForImportSite GetValueForImportSite(object part, RuntimeComposition.RuntimeImport import, Dictionary<int, object> provisionalSharedObjects)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

                var exports = import.SatisfyingExports;
                if (import.Cardinality == ImportCardinality.ZeroOrMore)
                {
                    if (import.ImportingSiteType.IsArray || (import.ImportingSiteType.GetTypeInfo().IsGenericType && import.ImportingSiteType.GetGenericTypeDefinition().IsEquivalentTo(typeof(IEnumerable<>))))
                    {
                        Array array = Array.CreateInstance(import.ImportingSiteTypeWithoutCollection, exports.Count);
                        using (var intArray = ArrayRental<int>.Get(1))
                        {
                            int i = 0;
                            foreach (var export in exports)
                            {
                                intArray.Value[0] = i++;
                                array.SetValue(this.GetValueForImportElement(part, import, export, provisionalSharedObjects), intArray.Value);
                            }
                        }

                        return new ValueForImportSite(array);
                    }
                    else
                    {
                        object collectionObject = null;
                        MemberInfo importingMember = import.ImportingMemberRef.Resolve();
                        if (importingMember != null)
                        {
                            collectionObject = GetImportingMember(part, importingMember);
                        }

                        bool preexistingInstance = collectionObject != null;
                        if (!preexistingInstance)
                        {
                            if (PartDiscovery.IsImportManyCollectionTypeCreateable(import.ImportingSiteType, import.ImportingSiteTypeWithoutCollection))
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

                                this.SetImportingMember(part, importingMember, collectionObject);
                            }
                            else
                            {
                                throw new CompositionFailedException("Unable to instantiate custom import collection type.");
                            }
                        }

                        var collectionAccessor = new CollectionWrapper(collectionObject, import.ImportingSiteTypeWithoutCollection);
                        if (preexistingInstance)
                        {
                            collectionAccessor.Clear();
                        }

                        foreach (var export in exports)
                        {
                            collectionAccessor.Add(this.GetValueForImportElement(part, import, export, provisionalSharedObjects));
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

                    return new ValueForImportSite(this.GetValueForImportElement(part, import, export, provisionalSharedObjects));
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

            private object GetValueForImportElement(object part, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, Dictionary<int, object> provisionalSharedObjects)
            {
                if (import.IsExportFactory)
                {
                    return this.CreateExportFactory(import, export);
                }
                else
                {
                    if (this.composition.GetPart(export).Type.Equals(import.DeclaringType))
                    {
                        return import.IsLazy
                            ? this.CreateStrongTypedLazy(() => part, export.Metadata, import.ImportingSiteTypeWithoutCollection)
                            : part;
                    }

                    Func<object> exportedValue = this.GetExportedValue(import, export, provisionalSharedObjects);

                    object importedValue = import.IsLazy
                        ? this.CreateStrongTypedLazy(exportedValue, export.Metadata, import.ImportingSiteTypeWithoutCollection)
                        : exportedValue();
                    return importedValue;
                }
            }

            private object CreateExportFactory(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(export, "export");

                Type importingSiteElementType = import.ImportingSiteElementType;
                IReadOnlyCollection<string> sharingBoundaries = import.ExportFactorySharingBoundaries;
                bool newSharingScope = sharingBoundaries.Count > 0;
                Func<KeyValuePair<object, IDisposable>> valueFactory = () =>
                {
                    RuntimeExportProvider scope = newSharingScope
                        ? new RuntimeExportProvider(this.composition, this, sharingBoundaries)
                        : this;
                    object constructedValue = ((RuntimeExportProvider)scope).GetExportedValue(import, export, new Dictionary<int, object>())();
                    var disposableValue = newSharingScope ? scope : constructedValue as IDisposable;
                    return new KeyValuePair<object, IDisposable>(constructedValue, disposableValue);
                };
                Type exportFactoryType = import.ExportFactory.Resolve();
                var exportMetadata = export.Metadata;

                return this.CreateExportFactory(importingSiteElementType, sharingBoundaries, valueFactory, exportFactoryType, exportMetadata);
            }

            private object CreateStrongTypedLazy(Func<object> valueFactory, IReadOnlyDictionary<string, object> metadata, Type lazyType)
            {
                Requires.NotNull(valueFactory, "valueFactory");
                Requires.NotNull(metadata, "metadata");

                lazyType = LazyPart.FromLazy(lazyType); // be sure we have a concrete type.
                using (var ctorArgs = ArrayRental<object>.Get(lazyType.GenericTypeArguments.Length))
                {
                    ctorArgs.Value[0] = valueFactory;
                    if (ctorArgs.Value.Length == 2)
                    {
                        ctorArgs.Value[1] = this.GetStrongTypedMetadata(metadata, lazyType.GenericTypeArguments[1]);
                    }

                    // We have to select and invoke the method directly because Activator.CreateInstance
                    // cannot pick the right type when the metadata value is a transparent proxy.
                    // Perf note: there are faster ways to select the constructor if this shows up on traces.
                    var lazyCtors = from ctor in lazyType.GetTypeInfo().DeclaredConstructors
                                    let parameters = ctor.GetParameters()
                                    where parameters.Length == ctorArgs.Value.Length
                                        && parameters[0].ParameterType == typeof(Func<object>)
                                        && (parameters.Length < 2 || parameters[1].ParameterType == lazyType.GenericTypeArguments[1])
                                    select ctor;
                    object lazyInstance = lazyCtors.First().Invoke(ctorArgs.Value);
                    return lazyInstance;
                }
            }

            private Func<object> GetExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, Dictionary<int, object> provisionalSharedObjects)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(export, "export");
                Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

                var exportingRuntimePart = this.composition.GetPart(export);

                // Special case importing of ExportProvider
                if (exportingRuntimePart.Type.Equals(ExportProvider.ExportProviderPartDefinition.Type))
                {
                    return this.NonDisposableWrapper.ValueFactory;
                }

                var constructedType = GetPartConstructedTypeRef(exportingRuntimePart, import.Metadata);

                Func<object> partFactory = this.GetOrCreateShareableValue(
                    this.GetTypeId(constructedType),
                    (ep, pso) => this.CreatePart(pso, exportingRuntimePart, import.Metadata),
                    provisionalSharedObjects,
                    exportingRuntimePart.SharingBoundary,
                    !exportingRuntimePart.IsShared || import.IsNonSharedInstanceRequired);
                MemberInfo exportingMember = export.Member.Resolve();
                Func<object> exportedValue = !export.Member.IsEmpty
                    ? () => this.GetValueFromMember(exportingMember.IsStatic() ? null : partFactory(), exportingMember, import.ImportingSiteElementType, export.ExportedValueType.Resolve())
                    : partFactory;
                return exportedValue;
            }

            /// <summary>
            /// Gets the constructed type (non generic type definition) for a part.
            /// </summary>
            private static Reflection.TypeRef GetPartConstructedTypeRef(RuntimeComposition.RuntimePart part, IReadOnlyDictionary<string, object> importMetadata)
            {
                Requires.NotNull(part, "part");
                Requires.NotNull(importMetadata, "importMetadata");

                if (part.Type.IsGenericTypeDefinition)
                {
                    var bareMetadata = LazyMetadataWrapper.TryUnwrap(importMetadata);
                    object typeArgsObject;
                    if (bareMetadata.TryGetValue(CompositionConstants.GenericParametersMetadataName, out typeArgsObject))
                    {
                        IEnumerable<Reflection.TypeRef> typeArgs = typeArgsObject as Reflection.TypeRef[];
                        if (typeArgs == null)
                        {
                            typeArgs = ((Type[])typeArgsObject).Select(t => Reflection.TypeRef.Get(t));
                        }

                        return part.Type.MakeGenericType(typeArgs.ToImmutableArray());
                    }
                }

                return part.Type;
            }

            private void SetImportingMember(object part, MemberInfo member, object value)
            {
                Requires.NotNull(part, "part");
                Requires.NotNull(member, "member");

                bool containsGenericParameters = member.DeclaringType.GetTypeInfo().ContainsGenericParameters;
                if (containsGenericParameters)
                {
                    member = ReflectionHelpers.CloseGenericType(member.DeclaringType, part.GetType()).GetTypeInfo().DeclaredMembers.First(m => m.Name == member.Name);
                }

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
        }
    }
}
