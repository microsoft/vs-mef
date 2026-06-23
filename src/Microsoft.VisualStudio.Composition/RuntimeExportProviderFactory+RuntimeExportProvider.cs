// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
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
                TypeRef.Get(typeof(IMetadataViewProvider), Resolver.DefaultInstance),
                ImportCardinality.ExactlyOne,
                ImmutableList<RuntimeComposition.RuntimeExport>.Empty,
                isNonSharedInstanceRequired: false,
                isExportFactory: false,
                metadata: ImmutableDictionary<string, object?>.Empty,
                exportFactorySharingBoundaries: ImmutableHashSet<string>.Empty);

            private readonly RuntimeComposition composition;
            private readonly ReportFaultCallback? faultCallback;

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

            private protected override IEnumerable<ExportInfo> GetExportsCore(ImportDefinition importDefinition)
            {
                var exports = this.composition.GetExports(importDefinition.ContractName);

                return
                    from export in exports
                    let part = this.composition.GetPart(export)
                    select this.CreateExport(
                        importDefinition,
                        export.Metadata,
                        part.TypeRef,
                        GetPartConstructedTypeRef(part, importDefinition.Metadata),
                        part.SharingBoundary,
                        !part.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinition),
                        export.MemberRef);
            }

            internal override PartLifecycleTracker CreatePartLifecycleTracker(TypeRef partType, IReadOnlyDictionary<string, object?> importMetadata, PartLifecycleTracker? nonSharedPartOwner)
            {
                return nonSharedPartOwner is object
                    ? new RuntimePartLifecycleTracker(this, this.composition.GetPart(partType), importMetadata, nonSharedPartOwner)
                    : new RuntimePartLifecycleTracker(this, this.composition.GetPart(partType), importMetadata);
            }

            internal override IMetadataViewProvider GetMetadataViewProvider(Type metadataView)
            {
                RuntimeComposition.RuntimeExport? metadataViewProviderExport;
                if (this.composition.MetadataViewsAndProviders.TryGetValue(TypeRef.Get(metadataView, this.Resolver), out metadataViewProviderExport))
                {
                    var result = (IMetadataViewProvider?)this.GetExportedValue(MetadataViewProviderImport, metadataViewProviderExport, importingPartTracker: null, out _);
                    Assumes.NotNull(result);
                    return result;
                }
                else
                {
                    return base.GetMetadataViewProvider(metadataView);
                }
            }

            private void ThrowIfExportedValueIsNotAssignableToImport(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, object? exportedValue)
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

                Func<AssemblyName, Func<object?>, object, object>? lazyFactory = import.LazyFactory;
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
                        object? collectionObject = null;
                        MemberInfo? importingMember = import.ImportingMember;
                        if (importingMember != null)
                        {
                            Assumes.NotNull(importingPartTracker.Value);
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
                                        collectionObject = Activator.CreateInstance(listType)!;
                                    }
                                    else
                                    {
                                        collectionObject = Activator.CreateInstance(import.ImportingSiteType)!;
                                    }
                                }

                                Assumes.NotNull(importingPartTracker.Value);
                                Assumes.NotNull(importingMember);
                                SetImportingMember(importingPartTracker.Value, importingMember, collectionObject);
                            }
                            else
                            {
                                throw new CompositionFailedException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Strings.UnableToInstantiateCustomImportCollectionType,
                                        import.ImportingSiteType.FullName,
                                        $"{import.DeclaringTypeRef.FullName}.{import.ImportingMemberRef?.Name}"));
                            }
                        }

                        var collectionAccessor = CollectionServices.GetCollectionWrapper(import.ImportingSiteTypeWithoutCollection, collectionObject!);
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

            private object? GetValueForImportElement(RuntimePartLifecycleTracker importingPartTracker, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, Func<AssemblyName, Func<object?>, object, object>? lazyFactory)
            {
                if (import.IsExportFactory)
                {
                    return this.CreateExportFactory(importingPartTracker, import, export);
                }
                else
                {
                    if (import.IsLazy)
                    {
                        Requires.NotNull(lazyFactory!, nameof(lazyFactory));
                    }

                    if (this.composition.GetPart(export).TypeRef.Equals(import.DeclaringTypeRef))
                    {
                        // This is importing itself.
                        object? part = importingPartTracker.Value;
                        object? value = import.IsLazy
                            ? lazyFactory!(export.DeclaringTypeRef.AssemblyName, () => part, this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
                            : part;
                        return value;
                    }

                    object? importedValue = import.IsLazy
                        ? lazyFactory!(export.DeclaringTypeRef.AssemblyName, this.GetLazyExportedValue(import, export, importingPartTracker), this.GetStrongTypedMetadata(export.Metadata, import.MetadataType ?? LazyServices.DefaultMetadataViewType))
                        : this.GetExportedValue(import, export, importingPartTracker, out _);
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
                Func<KeyValuePair<object?, IDisposable?>> valueFactory = () =>
                {
                    RuntimeExportProvider scope = newSharingScope
                        ? new RuntimeExportProvider(this.composition, this, sharingBoundaries)
                        : this;
                    object? constructedValue = scope.GetExportedValue(import, export, importingPartTracker, out PartLifecycleTracker? partLifecycle);
                    partLifecycle!.GetValueReadyToExpose();
                    var disposableValue = newSharingScope ? scope : partLifecycle as IDisposable;
                    return new KeyValuePair<object?, IDisposable?>(constructedValue, disposableValue);
                };
                Type? exportFactoryType = import.ImportingSiteTypeWithoutCollection!;
                var exportMetadata = export.Metadata;

                return this.CreateExportFactory(importingSiteElementType, sharingBoundaries, valueFactory, exportFactoryType, exportMetadata);
            }

            private Func<object?> GetLazyExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimePartLifecycleTracker? importingPartTracker)
            {
                return (Func<object?>)this.GetExportedValue(import, export, importingPartTracker, lazy: true, out _)!;
            }

            private object? GetExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimePartLifecycleTracker? importingPartTracker, out PartLifecycleTracker? partLifecycle)
            {
                return this.GetExportedValue(import, export, importingPartTracker, lazy: false, out partLifecycle);
            }

            private object? GetExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimePartLifecycleTracker? importingPartTracker, bool lazy, out PartLifecycleTracker? partLifecycle)
            {
                Requires.NotNull(import, nameof(import));
                Requires.NotNull(export, nameof(export));

                var exportingRuntimePart = this.composition.GetPart(export);

                if (this.TryHandleGetExportProvider(exportingRuntimePart, lazy, out object? exportProvider))
                {
                    partLifecycle = null;
                    return exportProvider;
                }

                // For static member exports, we don't need to create a part lifecycle tracker
                if (export.MemberRef != null && export.Member!.IsStatic())
                {
                    partLifecycle = null;
                    return lazy ? ConstructLazyStaticExportedValue(export) :
                                  ConstructStaticExportedValue(export);
                }

                var constructedType = GetPartConstructedTypeRef(exportingRuntimePart, import.Metadata);

                partLifecycle = this.GetOrCreateValue(import, exportingRuntimePart, exportingRuntimePart.TypeRef, constructedType, importingPartTracker);

                return lazy ? ConstructLazyExportedValue(import, export, importingPartTracker, partLifecycle, this.faultCallback) :
                              ConstructExportedValue(import, export, importingPartTracker, partLifecycle, this.faultCallback);

                static Func<object?> ConstructLazyStaticExportedValue(RuntimeComposition.RuntimeExport export)
                {
                    return () => ConstructStaticExportedValue(export);
                }

                static object? ConstructStaticExportedValue(RuntimeComposition.RuntimeExport export)
                {
                    return GetValueFromMember(null, export.Member!, null, export.ExportedValueTypeRef.Resolve());
                }

                static Func<object?> ConstructLazyExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimePartLifecycleTracker? importingPartTracker, PartLifecycleTracker partLifecycle, ReportFaultCallback? faultCallback)
                {
                    // Avoid inlining this method into its parent to avoid non-lazy path from paying for capture allocation
                    return () => ConstructExportedValue(import, export, importingPartTracker, partLifecycle, faultCallback);
                }
            }

            private bool TryHandleGetExportProvider(RuntimeComposition.RuntimePart exportingRuntimePart, bool lazy, out object? exportProvider)
            {
                Requires.NotNull(exportingRuntimePart, nameof(exportingRuntimePart));

                // Special case importing of ExportProvider
                if (exportingRuntimePart.TypeRef.Equals(ExportProvider.ExportProviderPartDefinition.TypeRef))
                {
                    exportProvider = lazy ? () => this.NonDisposableWrapper.Value :
                                                  this.NonDisposableWrapper.Value;

                    return true;
                }

                exportProvider = null;
                return false;
            }

            private PartLifecycleTracker GetOrCreateValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimePart exportingRuntimePart, TypeRef originalPartTypeRef, TypeRef constructedPartTypeRef, RuntimePartLifecycleTracker? importingPartTracker)
            {
                Requires.NotNull(import, nameof(import));
                Requires.NotNull(exportingRuntimePart, nameof(exportingRuntimePart));
                Requires.NotNull(originalPartTypeRef, nameof(originalPartTypeRef));
                Requires.NotNull(constructedPartTypeRef, nameof(constructedPartTypeRef));

                bool nonSharedInstanceRequired = !exportingRuntimePart.IsShared || import.IsNonSharedInstanceRequired;
                Requires.Argument(importingPartTracker is object || !nonSharedInstanceRequired, nameof(importingPartTracker), "Value required for non-shared parts.");
                RuntimePartLifecycleTracker? nonSharedPartOwner = nonSharedInstanceRequired && importingPartTracker!.IsNonShared && !import.IsExportFactory ? importingPartTracker : null;

                return this.GetOrCreateValue(
                    originalPartTypeRef,
                    constructedPartTypeRef,
                    exportingRuntimePart.SharingBoundary,
                    import.Metadata,
                    nonSharedInstanceRequired,
                    nonSharedPartOwner);
            }

            private static object? ConstructExportedValue(RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export, RuntimePartLifecycleTracker? importingPartTracker, PartLifecycleTracker partLifecycle, ReportFaultCallback? faultCallback)
            {
                Requires.NotNull(import, nameof(import));
                Requires.NotNull(export, nameof(export));
                Requires.NotNull(partLifecycle, nameof(partLifecycle));

                try
                {
                    bool fullyInitializedValueIsRequired = IsFullyInitializedExportRequiredWhenSettingImport(import.IsLazy, import.ImportingParameterRef != null);
                    if (!fullyInitializedValueIsRequired && importingPartTracker != null && !import.IsExportFactory)
                    {
                        importingPartTracker.ReportPartiallyInitializedImport(partLifecycle);
                    }

                    if (export.MemberRef != null)
                    {
                        object? part = export.Member!.IsStatic()
                            ? null
                            : (fullyInitializedValueIsRequired
                                ? partLifecycle.GetValueReadyToExpose()
                                : partLifecycle.GetValueReadyToRetrieveExportingMembers());
                        return GetValueFromMember(part, export.Member!, import.ImportingSiteElementType, export.ExportedValueTypeRef.Resolve());
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
            }

            /// <summary>
            /// Gets the constructed type (non generic type definition) for a part.
            /// </summary>
            private static Reflection.TypeRef GetPartConstructedTypeRef(RuntimeComposition.RuntimePart part, IReadOnlyDictionary<string, object?> importMetadata)
            {
                Requires.NotNull(part, nameof(part));
                Requires.NotNull(importMetadata, nameof(importMetadata));

                if (part.TypeRef.IsGenericTypeDefinition)
                {
                    var bareMetadata = LazyMetadataWrapper.TryUnwrap(importMetadata);
                    object? typeArgsObject;
                    if (bareMetadata.TryGetValue(CompositionConstants.GenericParametersMetadataName, out typeArgsObject) && typeArgsObject is object)
                    {
                        IEnumerable<TypeRef> typeArgs = typeArgsObject is LazyMetadataWrapper.TypeArraySubstitution
                            ? ((LazyMetadataWrapper.TypeArraySubstitution)typeArgsObject).TypeRefArray
                            : ReflectionHelpers.TypesToTypeRefs((Type[])typeArgsObject, part.TypeRef.Resolver);

                        return part.TypeRef.MakeGenericTypeRef(typeArgs.ToImmutableArray());
                    }
                }

                return part.TypeRef;
            }

            private static void SetImportingMember(object part, MemberInfo member, object? value)
            {
                Requires.NotNull(part, nameof(part));
                Requires.NotNull(member, nameof(member));
                Requires.Argument(member.DeclaringType is object, nameof(member), "DeclaringType must not be null.");

                bool containsGenericParameters = member.DeclaringType.GetTypeInfo().ContainsGenericParameters;
                if (containsGenericParameters)
                {
                    member = ReflectionHelpers.CloseGenericType(member.DeclaringType, part.GetType()).GetTypeInfo()
                        .GetMember(member.Name, MemberTypes.Property | MemberTypes.Field, DeclaredOnlyLookup)[0];
                }

                try
                {
                    switch (member)
                    {
                        case PropertyInfo property:
                            property.SetValue(part, value);
                            break;
                        case FieldInfo field:
                            field.SetValue(part, value);
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(Strings.FormatExceptionThrownByPartUnderInitialization(part.GetType().FullName), ex);
                }
            }

            private static object? GetImportingMember(object part, MemberInfo member)
            {
                Requires.NotNull(part, nameof(part));
                Requires.NotNull(member, nameof(member));

                try
                {
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
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(Strings.FormatExceptionThrownByPartUnderInitialization(part.GetType().FullName), ex);
                }

                throw new NotSupportedException();
            }

            private struct ValueForImportSite
            {
                internal ValueForImportSite(object? value)
                    : this()
                {
                    this.Value = value;
                    this.ValueShouldBeSet = true;
                }

                public bool ValueShouldBeSet { get; private set; }

                public object? Value { get; private set; }
            }

            [DebuggerDisplay("{" + nameof(partDefinition) + "." + nameof(RuntimeComposition.RuntimePart.TypeRef) + "." + nameof(TypeRef.ResolvedType) + ".FullName,nq} ({State})")]
            private class RuntimePartLifecycleTracker : PartLifecycleTracker
            {
                private readonly RuntimeComposition.RuntimePart partDefinition;
                private readonly IReadOnlyDictionary<string, object?> importMetadata;

                public RuntimePartLifecycleTracker(RuntimeExportProvider owningExportProvider, RuntimeComposition.RuntimePart partDefinition, IReadOnlyDictionary<string, object?> importMetadata)
                    : base(owningExportProvider, partDefinition.SharingBoundary)
                {
                    Requires.NotNull(partDefinition, nameof(partDefinition));
                    Requires.NotNull(importMetadata, nameof(importMetadata));

                    this.partDefinition = partDefinition;
                    this.importMetadata = importMetadata;
                }

                public RuntimePartLifecycleTracker(RuntimeExportProvider owningExportProvider, RuntimeComposition.RuntimePart partDefinition, IReadOnlyDictionary<string, object?> importMetadata, PartLifecycleTracker nonSharedPartOwner)
                    : base(owningExportProvider, nonSharedPartOwner)
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

                protected override object? CreateValue()
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

                    var constructedPartTypeRef = GetPartConstructedTypeRef(this.partDefinition, this.importMetadata);
                    var ctorArgs = this.partDefinition.ImportingConstructorArguments
                        .Select(import => this.OwningExportProvider.GetValueForImportSite(this, import).Value).ToArray();
                    MethodBase? importingConstructorOrFactoryMethod = this.partDefinition.ImportingConstructorOrFactoryMethod!;
                    if (importingConstructorOrFactoryMethod.ContainsGenericParameters)
                    {
                        MethodBase? importingConstructorOrFactoryMethodOnClosedGeneric = ReflectionHelpers.MapOpenGenericMemberToClosedGeneric(
                            importingConstructorOrFactoryMethod,
                            constructedPartTypeRef.Resolve().GetTypeInfo()) ?? throw ReflectionHelpers.ThrowUnsupportedImportingConstructor(importingConstructorOrFactoryMethod);
                        importingConstructorOrFactoryMethod = importingConstructorOrFactoryMethodOnClosedGeneric;
                    }

                    try
                    {
                        object? part = importingConstructorOrFactoryMethod.Instantiate(ctorArgs);
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
                                    SetImportingMember(this.Value!, import.ImportingMember!, value.Value);
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
                    if (this.partDefinition.OnImportsSatisfiedMethodRefs.Count > 0)
                    {
                        foreach (MethodRef method in this.partDefinition.OnImportsSatisfiedMethodRefs)
                        {
                            try
                            {
                                method.MethodBase.Invoke(this.Value, EmptyObjectArray);
                            }
                            catch (TargetInvocationException ex)
                            {
                                throw this.PrepareExceptionForFaultedPart(ex);
                            }
                        }
                    }
                }

                private Exception PrepareExceptionForFaultedPart(TargetInvocationException ex)
                {
                    // Discard the TargetInvocationException and throw a MEF related one, with the same inner exception.
                    return new CompositionFailedException(
                        Strings.FormatExceptionThrownByPartUnderInitialization(this.PartType.FullName),
                        ex.InnerException);
                }
            }
        }
    }
}
