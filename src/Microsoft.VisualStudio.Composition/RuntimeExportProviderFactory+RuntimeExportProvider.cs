// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal partial class RuntimeExportProviderFactory : IFaultReportingExportProviderFactory
    {
        private class RuntimeExportProvider : ExportProvider
        {
            /// <summary>
            /// BindingFlags that find members declared exactly on the receiving type, whether they be public or not, instance or static.
            /// </summary>
            private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            private static readonly RuntimeComposition.RuntimeImport MetadataViewProviderImport = new RuntimeComposition.RuntimeImport(
                default(MemberRef),
                TypeRef.Get(typeof(IMetadataViewProvider), Resolver.DefaultInstance),
                ImportCardinality.ExactlyOne,
                ImmutableList<RuntimeComposition.RuntimeExport>.Empty,
                isNonSharedInstanceRequired: false,
                isExportFactory: false,
                metadata: ImmutableDictionary<string, object>.Empty,
                exportFactorySharingBoundaries: ImmutableHashSet<string>.Empty);

            private readonly RuntimeComposition composition;
            private readonly ReportFaultCallback faultCallback;

            internal RuntimeExportProvider(RuntimeComposition composition, ReportFaultCallback faultCallback)
                : this(composition)
            {
                this.faultCallback = faultCallback;
            }

            internal RuntimeExportProvider(RuntimeComposition composition)
                : base(Requires.NotNull(composition, nameof(composition)).Resolver)
            {
                this.composition = composition;
            }

            internal RuntimeExportProvider(RuntimeComposition composition, ExportProvider parent, ImmutableHashSet<string> freshSharingBoundaries)
                : base(parent, freshSharingBoundaries)
            {
                Requires.NotNull(composition, nameof(composition));

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
                        part.TypeRef,
                        GetPartConstructedTypeRef(part, importDefinition.Metadata),
                        part.SharingBoundary,
                        !part.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinition),
                        export.Member);
            }

            protected internal override PartLifecycleTracker CreatePartLifecycleTracker(TypeRef partType, IReadOnlyDictionary<string, object> importMetadata)
            {
                return new RuntimePartLifecycleTracker(this, this.composition.GetPart(partType), importMetadata);
            }

            internal override IMetadataViewProvider GetMetadataViewProvider(Type metadataView)
            {
                RuntimeComposition.RuntimeExport metadataViewProviderExport;
                if (this.composition.MetadataViewsAndProviders.TryGetValue(TypeRef.Get(metadataView, this.Resolver), out metadataViewProviderExport))
                {
                    var export = this.GetExportedValue(MetadataViewProviderImport, metadataViewProviderExport, importingPartTracker: null);
                    return (IMetadataViewProvider)export.ValueConstructor();
                }
                else
                {
                    return base.GetMetadataViewProvider(metadataView);
                }
            }

            private void ThrowIfExportedValueIsNotAssignableToImport(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, object exportedValue)
            {
                Requires.NotNull(import, nameof(import));
                Requires.NotNull(export, nameof(export));

                if (exportedValue != null)
                {
                    if (!import.ImportingSiteTypeWithoutCollection.GetTypeInfo().IsAssignableFrom(exportedValue.GetType()))
                    {
                        throw new CompositionFailedException(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.ExportedValueNotAssignableToImport,
                                RuntimeComposition.GetDiagnosticLocation(export),
                                RuntimeComposition.GetDiagnosticLocation(import)));
                    }
                }
            }

            private ValueForImportSite GetValueForImportSite(RuntimePartLifecycleTracker importingPartTracker, RuntimeComposition.RuntimeImport import)
            {
                Requires.NotNull(import, nameof(import));

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
                                var exportedValue = this.GetValueForImportElement(importingPartTracker, import, export, lazyFactory);
                                this.ThrowIfExportedValueIsNotAssignableToImport(import, export, exportedValue);
                                array.SetValue(exportedValue, intArray.Value);
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
                            collectionObject = GetImportingMember(importingPartTracker.Value, importingMember);
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

                                SetImportingMember(importingPartTracker.Value, importingMember, collectionObject);
                            }
                            else
                            {
                                throw new CompositionFailedException(Strings.UnableToInstantiateCustomImportCollectionType);
                            }
                        }

                        var collectionAccessor = CollectionServices.GetCollectionWrapper(import.ImportingSiteTypeWithoutCollection, collectionObject);
                        if (preexistingInstance)
                        {
                            collectionAccessor.Clear();
                        }

                        foreach (var export in exports)
                        {
                            var exportedValue = this.GetValueForImportElement(importingPartTracker, import, export, lazyFactory);
                            this.ThrowIfExportedValueIsNotAssignableToImport(import, export, exportedValue);
                            collectionAccessor.Add(exportedValue);
                        }

                        return default(ValueForImportSite); // signal caller should not set value again.
                    }
                }
                else
                {
                    var export = exports.FirstOrDefault();
                    if (export == null)
                    {
                        return new ValueForImportSite(null);
                    }

                    var exportedValue = this.GetValueForImportElement(importingPartTracker, import, export, lazyFactory);
                    this.ThrowIfExportedValueIsNotAssignableToImport(import, export, exportedValue);
                    return new ValueForImportSite(exportedValue);
                }
            }

            private object GetValueForImportElement(RuntimePartLifecycleTracker importingPartTracker, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, Func<Func<object>, object, object> lazyFactory)
            {
                if (import.IsExportFactory)
                {
                    return this.CreateExportFactory(importingPartTracker, import, export);
                }
                else
                {
                    if (import.IsLazy)
                    {
                        Requires.NotNull(lazyFactory, nameof(lazyFactory));
                    }

                    if (this.composition.GetPart(export).TypeRef.Equals(import.DeclaringTypeRef))
                    {
                        // This is importing itself.
                        object part = importingPartTracker.Value;
                        object value = import.IsLazy
                            ? lazyFactory(() => part, this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
                            : part;
                        return value;
                    }

                    ExportedValueConstructor exportedValueConstructor = this.GetExportedValue(import, export, importingPartTracker);

                    object importedValue = import.IsLazy
                        ? lazyFactory(exportedValueConstructor.ValueConstructor, this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
                        : exportedValueConstructor.ValueConstructor();
                    return importedValue;
                }
            }

            private object CreateExportFactory(RuntimePartLifecycleTracker importingPartTracker, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export)
            {
                Requires.NotNull(importingPartTracker, nameof(importingPartTracker));
                Requires.NotNull(import, nameof(import));
                Requires.NotNull(export, nameof(export));

                Type importingSiteElementType = import.ImportingSiteElementType;
                ImmutableHashSet<string> sharingBoundaries = import.ExportFactorySharingBoundaries.ToImmutableHashSet();
                bool newSharingScope = sharingBoundaries.Count > 0;
                Func<KeyValuePair<object, IDisposable>> valueFactory = () =>
                {
                    RuntimeExportProvider scope = newSharingScope
                        ? new RuntimeExportProvider(this.composition, this, sharingBoundaries)
                        : this;
                    var exportedValueConstructor = ((RuntimeExportProvider)scope).GetExportedValue(import, export, importingPartTracker);
                    exportedValueConstructor.ExportingPart.GetValueReadyToExpose();
                    object constructedValue = exportedValueConstructor.ValueConstructor();
                    var disposableValue = newSharingScope ? scope : exportedValueConstructor.ExportingPart as IDisposable;
                    return new KeyValuePair<object, IDisposable>(constructedValue, disposableValue);
                };
                Type exportFactoryType = import.ImportingSiteTypeWithoutCollection;
                var exportMetadata = export.Metadata;

                return this.CreateExportFactory(importingSiteElementType, sharingBoundaries, valueFactory, exportFactoryType, exportMetadata);
            }

            private ExportedValueConstructor GetExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimePartLifecycleTracker importingPartTracker)
            {
                Requires.NotNull(import, nameof(import));
                Requires.NotNull(export, nameof(export));

                var exportingRuntimePart = this.composition.GetPart(export);

                // Special case importing of ExportProvider
                if (exportingRuntimePart.TypeRef.Equals(ExportProvider.ExportProviderPartDefinition.TypeRef))
                {
                    return new ExportedValueConstructor(null, () => this.NonDisposableWrapper.Value);
                }

                var constructedType = GetPartConstructedTypeRef(exportingRuntimePart, import.Metadata);

                return this.GetExportedValueHelper(import, export, exportingRuntimePart, exportingRuntimePart.TypeRef, constructedType, importingPartTracker);
            }

            /// <summary>
            /// Called from <see cref="GetExportedValue(RuntimeComposition.RuntimeImport, RuntimeComposition.RuntimeExport, RuntimePartLifecycleTracker)"/>
            /// only, as an assisting method. See remarks.
            /// </summary>
            /// <remarks>
            /// This method is separate from its one caller to avoid a csc.exe compiler bug
            /// where it captures "this" in the closure for exportedValue, resulting in a memory leak
            /// which caused one of our GC unit tests to fail.
            /// </remarks>
            private ExportedValueConstructor GetExportedValueHelper(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimeComposition.RuntimePart exportingRuntimePart, TypeRef originalPartTypeRef, TypeRef constructedPartTypeRef, RuntimePartLifecycleTracker importingPartTracker)
            {
                Requires.NotNull(import, nameof(import));
                Requires.NotNull(export, nameof(export));
                Requires.NotNull(exportingRuntimePart, nameof(exportingRuntimePart));
                Requires.NotNull(originalPartTypeRef, nameof(originalPartTypeRef));
                Requires.NotNull(constructedPartTypeRef, nameof(constructedPartTypeRef));

                PartLifecycleTracker partLifecycle = this.GetOrCreateValue(
                    originalPartTypeRef,
                    constructedPartTypeRef,
                    exportingRuntimePart.SharingBoundary,
                    import.Metadata,
                    !exportingRuntimePart.IsShared || import.IsNonSharedInstanceRequired);
                var faultCallback = this.faultCallback;

                Func<object> exportedValue = () =>
                {
                    try
                    {
                        bool fullyInitializedValueIsRequired = IsFullyInitializedExportRequiredWhenSettingImport(importingPartTracker, import.IsLazy, !import.ImportingParameterRef.IsEmpty);
                        if (!fullyInitializedValueIsRequired && importingPartTracker != null && !import.IsExportFactory)
                        {
                            importingPartTracker.ReportPartiallyInitializedImport(partLifecycle);
                        }

                        if (!export.MemberRef.IsEmpty)
                        {
                            object part = export.Member.IsStatic()
                                ? null
                                : (fullyInitializedValueIsRequired
                                    ? partLifecycle.GetValueReadyToExpose()
                                    : partLifecycle.GetValueReadyToRetrieveExportingMembers());
                            return GetValueFromMember(part, export.Member, import.ImportingSiteElementType, export.ExportedValueTypeRef.Resolve());
                        }
                        else
                        {
                            return fullyInitializedValueIsRequired
                                ? partLifecycle.GetValueReadyToExpose()
                                : partLifecycle.GetValueReadyToRetrieveExportingMembers();
                        }
                    }
                    catch (Exception e)
                    {
                        // Let the MEF host know that an exception has been thrown while resolving an exported value
                        faultCallback?.Invoke(e, import, export);
                        throw;
                    }
                };

                return new ExportedValueConstructor(partLifecycle, exportedValue);
            }

            /// <summary>
            /// Gets the constructed type (non generic type definition) for a part.
            /// </summary>
            private static Reflection.TypeRef GetPartConstructedTypeRef(RuntimeComposition.RuntimePart part, IReadOnlyDictionary<string, object> importMetadata)
            {
                Requires.NotNull(part, nameof(part));
                Requires.NotNull(importMetadata, nameof(importMetadata));

                if (part.TypeRef.IsGenericTypeDefinition)
                {
                    var bareMetadata = LazyMetadataWrapper.TryUnwrap(importMetadata);
                    object typeArgsObject;
                    if (bareMetadata.TryGetValue(CompositionConstants.GenericParametersMetadataName, out typeArgsObject))
                    {
                        IEnumerable<TypeRef> typeArgs = typeArgsObject is LazyMetadataWrapper.TypeArraySubstitution
                            ? ((LazyMetadataWrapper.TypeArraySubstitution)typeArgsObject).TypeRefArray
                            : ((Type[])typeArgsObject).Select(t => TypeRef.Get(t, part.TypeRef.Resolver));

                        return part.TypeRef.MakeGenericTypeRef(typeArgs.ToImmutableArray());
                    }
                }

                return part.TypeRef;
            }

            private static void SetImportingMember(object part, MemberInfo member, object value)
            {
                Requires.NotNull(part, nameof(part));
                Requires.NotNull(member, nameof(member));

                bool containsGenericParameters = member.DeclaringType.GetTypeInfo().ContainsGenericParameters;
                if (containsGenericParameters)
                {
                    member = ReflectionHelpers.CloseGenericType(member.DeclaringType, part.GetType()).GetTypeInfo()
                        .GetMember(member.Name, MemberTypes.Property | MemberTypes.Field, DeclaredOnlyLookup)[0];
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
                Requires.NotNull(part, nameof(part));
                Requires.NotNull(member, nameof(member));

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

            private struct ExportedValueConstructor
            {
                public ExportedValueConstructor(PartLifecycleTracker exportingPart, Func<object> valueConstructor)
                    : this()
                {
                    Requires.NotNull(valueConstructor, nameof(valueConstructor));

                    this.ExportingPart = exportingPart;
                    this.ValueConstructor = valueConstructor;
                }

                public Func<object> ValueConstructor { get; private set; }

                public PartLifecycleTracker ExportingPart { get; private set; }
            }

            [DebuggerDisplay("{" + nameof(partDefinition) + "." + nameof(RuntimeComposition.RuntimePart.TypeRef) + "." + nameof(TypeRef.ResolvedType) + ".FullName,nq} ({State})")]
            private class RuntimePartLifecycleTracker : PartLifecycleTracker
            {
                private readonly RuntimeComposition.RuntimePart partDefinition;
                private readonly IReadOnlyDictionary<string, object> importMetadata;

                public RuntimePartLifecycleTracker(RuntimeExportProvider owningExportProvider, RuntimeComposition.RuntimePart partDefinition, IReadOnlyDictionary<string, object> importMetadata)
                    : base(owningExportProvider, partDefinition.SharingBoundary)
                {
                    Requires.NotNull(partDefinition, nameof(partDefinition));
                    Requires.NotNull(importMetadata, nameof(importMetadata));

                    this.partDefinition = partDefinition;
                    this.importMetadata = importMetadata;
                }

                protected new RuntimeExportProvider OwningExportProvider
                {
                    get { return (RuntimeExportProvider)base.OwningExportProvider; }
                }

                protected Resolver Resolver => this.OwningExportProvider.Resolver;

                /// <summary>
                /// Gets the type that backs this part.
                /// </summary>
                protected override Type PartType
                {
                    get { return this.partDefinition.TypeRef.Resolve(); }
                }

                internal new void ReportPartiallyInitializedImport(PartLifecycleTracker part)
                {
                    base.ReportPartiallyInitializedImport(part);
                }

                protected override object CreateValue()
                {
                    if (this.partDefinition.TypeRef.Equals(ExportProviderPartDefinition.TypeRef))
                    {
                        // Special case for our synthesized part that acts as a placeholder for *this* export provider.
                        return this.OwningExportProvider.NonDisposableWrapper.Value;
                    }

                    if (!this.partDefinition.IsInstantiable)
                    {
                        return null;
                    }

                    var constructedPartType = GetPartConstructedTypeRef(this.partDefinition, this.importMetadata);
                    var ctorArgs = this.partDefinition.ImportingConstructorArguments
                        .Select(import => this.OwningExportProvider.GetValueForImportSite(this, import).Value).ToArray();
                    ConstructorInfo importingConstructor = this.partDefinition.ImportingConstructor;
                    if (importingConstructor.ContainsGenericParameters)
                    {
                        // TODO: fix this to find the precise match, including cases where the matching constructor includes a generic type parameter.
                        importingConstructor = constructedPartType.Resolve().GetTypeInfo().DeclaredConstructors.First(ctor => true);
                    }

                    try
                    {
                        object part = importingConstructor.Invoke(ctorArgs);
                        return part;
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw this.PrepareExceptionForFaultedPart(ex);
                    }
                }

                protected override void SatisfyImports()
                {
                    if (this.Value == null && this.partDefinition.ImportingMembers.Count > 0)
                    {
                        // The value should have been instantiated by now. If it hasn't been,
                        // it's not an instantiable part. And such a part cannot have imports set.
                        this.ThrowPartNotInstantiableException();
                    }

                    try
                    {
                        foreach (var import in this.partDefinition.ImportingMembers)
                        {
                            try
                            {
                                ValueForImportSite value = this.OwningExportProvider.GetValueForImportSite(this, import);
                                if (value.ValueShouldBeSet)
                                {
                                    SetImportingMember(this.Value, import.ImportingMember, value.Value);
                                }
                            }
                            catch (CompositionFailedException ex)
                            {
                                throw new CompositionFailedException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Strings.ErrorWhileSettingImport,
                                        RuntimeComposition.GetDiagnosticLocation(import)),
                                    ex);
                            }
                        }
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw this.PrepareExceptionForFaultedPart(ex);
                    }
                }

                protected override void InvokeOnImportsSatisfied()
                {
                    if (this.partDefinition.OnImportsSatisfied != null)
                    {
                        try
                        {
                            this.partDefinition.OnImportsSatisfied.Invoke(this.Value, EmptyObjectArray);
                        }
                        catch (TargetInvocationException ex)
                        {
                            throw this.PrepareExceptionForFaultedPart(ex);
                        }
                    }
                }

                private Exception PrepareExceptionForFaultedPart(TargetInvocationException ex)
                {
                    // Discard the TargetInvocationException and throw a MEF related one, with the same inner exception.
                    return new CompositionFailedException(
                        string.Format(CultureInfo.CurrentCulture, Strings.ExceptionThrownByPartUnderInitialization, this.PartType.FullName),
                        ex.InnerException);
                }
            }
        }
    }
}
