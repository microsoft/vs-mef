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

    partial class RuntimeExportProviderFactory : IExportProviderFactory
    {
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
                        GetPartConstructedTypeRef(part, importDefinition.Metadata),
                        (ep, provisionalSharedObjects, nonSharedInstanceRequired) => this.CreatePart(provisionalSharedObjects, part, importDefinition.Metadata, nonSharedInstanceRequired),
                        part.SharingBoundary,
                        !part.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinition),
                        export.Member);
            }

            private object CreatePart(Dictionary<TypeRef, object> provisionalSharedObjects, RuntimeComposition.RuntimePart partDefinition, IReadOnlyDictionary<string, object> importMetadata, bool nonSharedInstanceRequired)
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
                ConstructorInfo importingConstructor = partDefinition.ImportingConstructor;
                if (importingConstructor.ContainsGenericParameters)
                {
                    // TODO: fix this to find the precise match, including cases where the matching constructor includes a generic type parameter.
                    importingConstructor = constructedPartType.Resolve().GetTypeInfo().DeclaredConstructors.First(ctor => true);
                }

                object part = importingConstructor.Invoke(ctorArgs);

                if (partDefinition.IsShared && !nonSharedInstanceRequired)
                {
                    lock (provisionalSharedObjects)
                    {
                        provisionalSharedObjects.Add(constructedPartType, part);
                    }
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
                        this.SetImportingMember(part, import.ImportingMember, value.Value);
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

            private ValueForImportSite GetValueForImportSite(object part, RuntimeComposition.RuntimeImport import, Dictionary<TypeRef, object> provisionalSharedObjects)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

                Func<Func<object>, object, object> lazyFactory = import.LazyFactory;
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
                                array.SetValue(this.GetValueForImportElement(part, import, export, provisionalSharedObjects, lazyFactory), intArray.Value);
                            }
                        }

                        return new ValueForImportSite(array);
                    }
                    else
                    {
                        object collectionObject = null;
                        MemberInfo importingMember = import.ImportingMember;
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

                        var collectionAccessor = CollectionServices.GetCollectionWrapper(import.ImportingSiteTypeWithoutCollection, collectionObject);
                        if (preexistingInstance)
                        {
                            collectionAccessor.Clear();
                        }

                        foreach (var export in exports)
                        {
                            collectionAccessor.Add(this.GetValueForImportElement(part, import, export, provisionalSharedObjects, lazyFactory));
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

                    return new ValueForImportSite(this.GetValueForImportElement(part, import, export, provisionalSharedObjects, lazyFactory));
                }
            }

            private object GetValueForImportElement(object part, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, Dictionary<TypeRef, object> provisionalSharedObjects, Func<Func<object>, object, object> lazyFactory)
            {
                if (import.IsExportFactory)
                {
                    return this.CreateExportFactory(import, export);
                }
                else
                {
                    if (import.IsLazy)
                    {
                        Requires.NotNull(lazyFactory, "lazyFactory");
                    }

                    if (this.composition.GetPart(export).Type.Equals(import.DeclaringType))
                    {
                        return import.IsLazy
                            ? lazyFactory(() => part, this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
                            : part;
                    }

                    Func<object> exportedValue = this.GetExportedValue(import, export, provisionalSharedObjects);

                    object importedValue = import.IsLazy
                        ? lazyFactory(exportedValue, this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
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
                    object constructedValue = ((RuntimeExportProvider)scope).GetExportedValue(import, export, new Dictionary<TypeRef, object>())();
                    var disposableValue = newSharingScope ? scope : constructedValue as IDisposable;
                    return new KeyValuePair<object, IDisposable>(constructedValue, disposableValue);
                };
                Type exportFactoryType = import.ImportingSiteTypeWithoutCollection;
                var exportMetadata = export.Metadata;

                return this.CreateExportFactory(importingSiteElementType, sharingBoundaries, valueFactory, exportFactoryType, exportMetadata);
            }

            private Func<object> GetExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, Dictionary<TypeRef, object> provisionalSharedObjects)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(export, "export");
                Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

                var exportingRuntimePart = this.composition.GetPart(export);

                // Special case importing of ExportProvider
                if (exportingRuntimePart.Type.Equals(ExportProvider.ExportProviderPartDefinition.Type))
                {
                    return () => this.NonDisposableWrapper.Value;
                }

                var constructedType = GetPartConstructedTypeRef(exportingRuntimePart, import.Metadata);

                Func<object> partFactory = this.GetOrCreateShareableValue(
                    constructedType,
                    (ep, pso, nonSharedInstanceRequired) => this.CreatePart(pso, exportingRuntimePart, import.Metadata, nonSharedInstanceRequired),
                    provisionalSharedObjects,
                    exportingRuntimePart.SharingBoundary,
                    !exportingRuntimePart.IsShared || import.IsNonSharedInstanceRequired);
                Func<object> exportedValue = !export.MemberRef.IsEmpty
                    ? () => this.GetValueFromMember(export.Member.IsStatic() ? null : partFactory(), export.Member, import.ImportingSiteElementType, export.ExportedValueType.Resolve())
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
