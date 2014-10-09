namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
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
                    let isValueFactoryRequired = export.Member == null || !export.Member.IsStatic()
                    select this.CreateExport(
                        importDefinition,
                        export.Metadata,
                        GetPartConstructedTypeRef(part, importDefinition.Metadata),
                        part.SharingBoundary,
                        !part.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinition),
                        export.Member);
            }

            protected internal override PartLifecycleTracker CreatePartLifecycleTracker(TypeRef partType, IReadOnlyDictionary<string, object> importMetadata)
            {
                return new RuntimePartLifecycleTracker(this, this.composition.GetPart(partType), importMetadata);
            }

            private static readonly RuntimeComposition.RuntimeImport metadataViewProviderImport = new RuntimeComposition.RuntimeImport(
                default(MemberRef),
                TypeRef.Get(typeof(IMetadataViewProvider)),
                ImportCardinality.ExactlyOne,
                ImmutableList<RuntimeComposition.RuntimeExport>.Empty,
                isNonSharedInstanceRequired: false,
                isExportFactory: false,
                metadata: ImmutableDictionary<string, object>.Empty,
                exportFactorySharingBoundaries: ImmutableHashSet<string>.Empty);

            internal override IMetadataViewProvider GetMetadataViewProvider(Type metadataView)
            {
                RuntimeComposition.RuntimeExport metadataViewProviderExport;
                if (this.composition.MetadataViewsAndProviders.TryGetValue(TypeRef.Get(metadataView), out metadataViewProviderExport))
                {
                    var export = GetExportedValue(metadataViewProviderImport, metadataViewProviderExport);
                    return (IMetadataViewProvider)export.ValueConstructor();
                }
                else
                {
                    return base.GetMetadataViewProvider(metadataView);
                }
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

            private ValueForImportSite GetValueForImportSite(object part, RuntimeComposition.RuntimeImport import, RuntimePartLifecycleTracker tracker)
            {
                Requires.NotNull(import, "import");

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
                                var exportedValue = this.GetValueForImportElement(part, import, export, lazyFactory);
                                tracker.ReportImportedPart(exportedValue.ExportingPart);
                                array.SetValue(exportedValue.Value, intArray.Value);
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

                                SetImportingMember(part, importingMember, collectionObject);
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
                            var exportedValue = this.GetValueForImportElement(part, import, export, lazyFactory);
                            tracker.ReportImportedPart(exportedValue.ExportingPart);
                            collectionAccessor.Add(exportedValue.Value);
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

                    var exportedValue = this.GetValueForImportElement(part, import, export, lazyFactory);
                    tracker.ReportImportedPart(exportedValue.ExportingPart);
                    return new ValueForImportSite(exportedValue.Value);
                }
            }

            private ExportedValue GetValueForImportElement(object part, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, Func<Func<object>, object, object> lazyFactory)
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
                        // This is importing itself.
                        object value = import.IsLazy
                            ? lazyFactory(() => part, this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
                            : part;
                        return new ExportedValue(null, value);
                    }

                    ExportedValueConstructor exportedValueConstructor = this.GetExportedValue(import, export);

                    object importedValue = import.IsLazy
                        ? lazyFactory(exportedValueConstructor.ValueConstructor, this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
                        : exportedValueConstructor.ValueConstructor();
                    return new ExportedValue(exportedValueConstructor.ExportingPart, importedValue);
                }
            }

            private ExportedValue CreateExportFactory(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export)
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
                    var exportedValueConstructor = ((RuntimeExportProvider)scope).GetExportedValue(import, export);
                    exportedValueConstructor.ExportingPart.GetValueReadyToExpose();
                    object constructedValue = exportedValueConstructor.ValueConstructor();
                    var disposableValue = newSharingScope ? scope : constructedValue as IDisposable;
                    return new KeyValuePair<object, IDisposable>(constructedValue, disposableValue);
                };
                Type exportFactoryType = import.ImportingSiteTypeWithoutCollection;
                var exportMetadata = export.Metadata;

                return new ExportedValue(
                    null,
                    this.CreateExportFactory(importingSiteElementType, sharingBoundaries, valueFactory, exportFactoryType, exportMetadata));
            }

            private ExportedValueConstructor GetExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export)
            {
                Requires.NotNull(import, "import");
                Requires.NotNull(export, "export");

                var exportingRuntimePart = this.composition.GetPart(export);

                // Special case importing of ExportProvider
                if (exportingRuntimePart.Type.Equals(ExportProvider.ExportProviderPartDefinition.TypeRef))
                {
                    return new ExportedValueConstructor(null, () => this.NonDisposableWrapper.Value);
                }

                var constructedType = GetPartConstructedTypeRef(exportingRuntimePart, import.Metadata);

                return GetExportedValueHelper(import, export, exportingRuntimePart, constructedType);
            }

            /// <remarks>
            /// This method is separate from its one caller to avoid a csc.exe compiler bug
            /// where it captures "this" in the closure for exportedValue, resulting in a memory leak
            /// which caused one of our GC unit tests to fail.
            /// </remarks>
            private ExportedValueConstructor GetExportedValueHelper(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimeComposition.RuntimePart exportingRuntimePart, TypeRef constructedType)
            {
                PartLifecycleTracker partLifecycle = this.GetOrCreateShareableValue(
                    constructedType,
                    exportingRuntimePart.SharingBoundary,
                    import.Metadata,
                    !exportingRuntimePart.IsShared || import.IsNonSharedInstanceRequired);
                Func<object> exportedValue = !export.MemberRef.IsEmpty
                    ? () => GetValueFromMember(export.Member.IsStatic() ? null : partLifecycle.GetValueReadyToRetrieveExportingMembers(), export.Member, import.ImportingSiteElementType, export.ExportedValueType.Resolve())
                    : new Func<object>(partLifecycle.GetValueReadyToRetrieveExportingMembers);
                return new ExportedValueConstructor(partLifecycle, exportedValue);
            }

            private struct ExportedValueConstructor
            {
                public ExportedValueConstructor(PartLifecycleTracker exportingPart, Func<object> valueConstructor)
                    : this()
                {
                    Requires.NotNull(exportingPart, "exportingPart");
                    Requires.NotNull(valueConstructor, "valueConstructor");

                    this.ExportingPart = exportingPart;
                    this.ValueConstructor = valueConstructor;
                }

                public Func<object> ValueConstructor { get; private set; }

                public PartLifecycleTracker ExportingPart { get; private set; }
            }

            private struct ExportedValue
            {
                public ExportedValue(PartLifecycleTracker exportingPart, object value)
                    : this()
                {
                    Requires.NotNull(value, "value");

                    this.ExportingPart = exportingPart;
                    this.Value = value;
                }

                public object Value { get; private set; }

                /// <summary>
                /// Gets the lifecycle tracker for the exporting part.
                /// </summary>
                /// <value>May be null when not applicable, such as for built-in values and export factories.</value>
                public PartLifecycleTracker ExportingPart { get; private set; }
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
                        IEnumerable<TypeRef> typeArgs = typeArgsObject is LazyMetadataWrapper.TypeArraySubstitution
                            ? ((LazyMetadataWrapper.TypeArraySubstitution)typeArgsObject).TypeRefArray
                            : ((Type[])typeArgsObject).Select(t => TypeRef.Get(t));

                        return part.Type.MakeGenericType(typeArgs.ToImmutableArray());
                    }
                }

                return part.Type;
            }

            private static void SetImportingMember(object part, MemberInfo member, object value)
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

            [DebuggerDisplay("{partDefinition.Type.ResolvedType.FullName,nq}")]
            private class RuntimePartLifecycleTracker : PartLifecycleTracker
            {
                private readonly RuntimeComposition.RuntimePart partDefinition;
                private readonly IReadOnlyDictionary<string, object> importMetadata;

                public RuntimePartLifecycleTracker(RuntimeExportProvider owningExportProvider, RuntimeComposition.RuntimePart partDefinition, IReadOnlyDictionary<string, object> importMetadata)
                    : base(owningExportProvider, partDefinition.SharingBoundary)
                {
                    Requires.NotNull(partDefinition, "partDefinition");
                    Requires.NotNull(importMetadata, "importMetadata");

                    this.partDefinition = partDefinition;
                    this.importMetadata = importMetadata;
                }

                internal new void ReportImportedPart(PartLifecycleTracker part)
                {
                    base.ReportImportedPart(part);
                }

                protected new RuntimeExportProvider OwningExportProvider
                {
                    get { return (RuntimeExportProvider)base.OwningExportProvider; }
                }

                protected override object CreateValue()
                {
                    if (this.partDefinition.Type.Equals(Reflection.TypeRef.Get(ExportProvider.ExportProviderPartDefinition.Type)))
                    {
                        // Special case for our synthesized part that acts as a placeholder for *this* export provider.
                        return this.OwningExportProvider.NonDisposableWrapper.Value;
                    }

                    if (!this.partDefinition.IsInstantiable)
                    {
                        throw new CompositionFailedException("Cannot instantiate this part.");
                    }

                    var constructedPartType = GetPartConstructedTypeRef(this.partDefinition, this.importMetadata);
                    var ctorArgs = this.partDefinition.ImportingConstructorArguments
                        .Select(import => this.OwningExportProvider.GetValueForImportSite(null, import, this).Value).ToArray();
                    ConstructorInfo importingConstructor = this.partDefinition.ImportingConstructor;
                    if (importingConstructor.ContainsGenericParameters)
                    {
                        // TODO: fix this to find the precise match, including cases where the matching constructor includes a generic type parameter.
                        importingConstructor = constructedPartType.Resolve().GetTypeInfo().DeclaredConstructors.First(ctor => true);
                    }

                    object part = importingConstructor.Invoke(ctorArgs);
                    return part;
                }

                protected override void SatisfyImports()
                {
                    foreach (var import in this.partDefinition.ImportingMembers)
                    {
                        ValueForImportSite value = this.OwningExportProvider.GetValueForImportSite(this.Value, import, this);
                        if (value.ValueShouldBeSet)
                        {
                            SetImportingMember(this.Value, import.ImportingMember, value.Value);
                        }
                    }
                }

                protected override void InvokeOnImportsSatisfied()
                {
                    if (this.partDefinition.OnImportsSatisfied != null)
                    {
                        this.partDefinition.OnImportsSatisfied.Invoke(this.Value, EmptyObjectArray);
                    }
                }
            }
        }
    }
}
