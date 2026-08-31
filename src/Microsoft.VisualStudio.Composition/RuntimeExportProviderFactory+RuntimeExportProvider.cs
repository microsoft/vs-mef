// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal partial class RuntimeExportProviderFactory : IFaultReportingExportProviderFactory
    {
        private class RuntimeExportProvider : ExportProvider
        {
            /// <summary>
            /// BindingFlags that find members declared exactly on the receiving type, whether they be public or not, instance or static.
            /// </summary>
            private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            private static readonly MethodInfo CreateFusedImportAssignmentExceptionMethod = typeof(RuntimeExportProvider).GetMethod(
                nameof(CreateFusedImportAssignmentException),
                BindingFlags.Static | BindingFlags.NonPublic)!;

            private static readonly MethodInfo CreateFusedPartActivationExceptionMethod = typeof(RuntimeExportProvider).GetMethod(
                nameof(CreateFusedPartActivationException),
                BindingFlags.Static | BindingFlags.NonPublic)!;

            private static readonly MethodInfo GetValueForImportSiteMethod = typeof(RuntimeExportProvider).GetMethod(
                nameof(GetValueForImportSite),
                BindingFlags.Instance | BindingFlags.NonPublic)!;

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

            private readonly ActivationPlanRegistry activationPlanRegistry;
            private readonly RuntimeComposition composition;
            private readonly ReportFaultCallback? faultCallback;
            private readonly ConcurrentDictionary<(Type Type, string? ContractName), RuntimeExportLookup> runtimeExportLookupCache = new();

            internal RuntimeExportProvider(RuntimeComposition composition, ActivationPlanRegistry activationPlanRegistry, ReportFaultCallback faultCallback)
                : this(composition, activationPlanRegistry)
            {
                this.faultCallback = faultCallback;
            }

            internal RuntimeExportProvider(RuntimeComposition composition, ActivationPlanRegistry activationPlanRegistry)
                : base(Requires.NotNull(composition, nameof(composition)).Resolver)
            {
                Requires.NotNull(activationPlanRegistry, nameof(activationPlanRegistry));

                this.composition = composition;
                this.activationPlanRegistry = activationPlanRegistry;
            }

            internal RuntimeExportProvider(RuntimeComposition composition, ActivationPlanRegistry activationPlanRegistry, ExportProvider parent, ImmutableHashSet<string> freshSharingBoundaries)
                : base(parent, freshSharingBoundaries)
            {
                Requires.NotNull(composition, nameof(composition));
                Requires.NotNull(activationPlanRegistry, nameof(activationPlanRegistry));

                this.composition = composition;
                this.activationPlanRegistry = activationPlanRegistry;
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                try
                {
                    base.Dispose(disposing);
                }
                finally
                {
                    if (disposing)
                    {
                        this.runtimeExportLookupCache.Clear();
                    }
                }
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

            private protected override bool TryGetExportedValue(Type type, string? contractName, out object? value)
            {
                Verify.NotDisposed(this);

                contractName = string.IsNullOrEmpty(contractName) ? null : contractName;
                if (!this.runtimeExportLookupCache.TryGetValue((type, contractName), out RuntimeExportLookup? lookup))
                {
                    lookup = this.CreateRuntimeExportLookup(type, contractName ?? ContractNameServices.GetTypeIdentity(type));
                    lookup = this.runtimeExportLookupCache.GetOrAdd((type, contractName), lookup);
                }

                Assumes.NotNull(lookup);
                if (!lookup.CanUseFastPath)
                {
                    value = null;
                    return false;
                }

                if (lookup.TryGetSharedValue(out value))
                {
                    return true;
                }

                value = this.GetRuntimeExportedValue(lookup, type, out bool isFullyInitialized);
                if (isFullyInitialized)
                {
                    lookup.SetSharedValue(value);
                }

                return true;
            }

            private object? GetRuntimeExportedValue(RuntimeExportLookup lookup, Type type, out bool isFullyInitialized)
            {
                if (!lookup.Part!.IsShared
                    && (lookup.TryGetDirectActivationPlan(out DirectActivationPlan? directActivationPlan)
                        || (lookup.Export!.MemberRef is null
                            && this.activationPlanRegistry.IsExpressionCompilationEnabled
                            && this.activationPlanRegistry.TryGetExistingDirectActivationPlan(lookup.Part, out directActivationPlan)
                            && lookup.SetDirectActivationPlan(directActivationPlan))))
                {
                    isFullyInitialized = true;
                    return directActivationPlan.Activate(this, importingPartTracker: null);
                }

                isFullyInitialized = false;
                MemberInfo? exportingMember = lookup.Export!.Member;
                if (exportingMember?.IsStatic() == true)
                {
                    return GetValueFromMember(null, exportingMember, type, lookup.Export.ExportedValueTypeRef.Resolve());
                }

                PartLifecycleTracker partLifecycle = this.GetOrCreateValue(
                    lookup.Part!.TypeRef,
                    lookup.Part.TypeRef,
                    lookup.Part.SharingBoundary,
                    EmptyMetadata,
                    !lookup.Part.IsShared,
                    nonSharedPartOwner: null);
                object? part = partLifecycle.GetValueReadyToExpose();
                isFullyInitialized = partLifecycle.State == PartLifecycleState.Final;
                return lookup.Export.MemberRef is null
                    ? part
                    : GetValueFromMember(part, exportingMember!, type, lookup.Export.ExportedValueTypeRef.Resolve());
            }

            internal override PartLifecycleTracker CreatePartLifecycleTracker(TypeRef partType, IReadOnlyDictionary<string, object?> importMetadata, PartLifecycleTracker? nonSharedPartOwner)
            {
                RuntimeComposition.RuntimePart part = this.composition.GetPart(partType);
                if (this.activationPlanRegistry.IsExpressionCompilationEnabled
                    && this.activationPlanRegistry.TryGetDirectActivationPlan(this, part, out DirectActivationPlan? activationPlan))
                {
                    return nonSharedPartOwner is object
                        ? new DirectActivationRuntimePartLifecycleTracker(this, part, importMetadata, nonSharedPartOwner, activationPlan)
                        : new DirectActivationRuntimePartLifecycleTracker(this, part, importMetadata, activationPlan);
                }

                return nonSharedPartOwner is object
                    ? new RuntimePartLifecycleTracker(this, part, importMetadata, nonSharedPartOwner)
                    : new RuntimePartLifecycleTracker(this, part, importMetadata);
            }

            private RuntimeExportLookup CreateRuntimeExportLookup(Type type, string contractName)
            {
                if (type.IsEquivalentTo(typeof(object)))
                {
                    return RuntimeExportLookup.Unsupported;
                }

                IReadOnlyCollection<RuntimeComposition.RuntimeExport> exports = this.composition.GetExports(contractName);
                string typeIdentity = ContractNameServices.GetTypeIdentity(type);
                RuntimeComposition.RuntimeExport? matchingExport = null;
                int matchingExportCount = 0;
                foreach (RuntimeComposition.RuntimeExport export in exports)
                {
                    if (export.Metadata.TryGetValue(CompositionConstants.ExportTypeIdentityMetadataName, out object? exportedTypeIdentity)
                        && string.Equals(typeIdentity, exportedTypeIdentity as string, StringComparison.Ordinal))
                    {
                        matchingExport = export;
                        matchingExportCount++;
                    }
                }

                if (matchingExportCount != 1 || matchingExport!.MemberRef is object)
                {
                    return RuntimeExportLookup.Unsupported;
                }

                RuntimeComposition.RuntimePart part = this.composition.GetPart(matchingExport);
                return part.TypeRef.IsGenericTypeDefinition
                    ? RuntimeExportLookup.Unsupported
                    : new RuntimeExportLookup(part, matchingExport);
            }

            internal bool TryCreateDirectActivationPlan(
                RuntimeComposition.RuntimePart part,
                HashSet<TypeRef> partsBeingBuilt,
                [NotNullWhen(true)] out DirectActivationPlan? activationPlan)
            {
                activationPlan = null;
                if (part.SharingBoundary?.Length == 0
                    || !part.IsInstantiable
                    || part.TypeRef.IsGenericTypeDefinition
                    || part.OnImportsSatisfiedMethodRefs.Count > 0
                    || typeof(IDisposable).GetTypeInfo().IsAssignableFrom(part.TypeRef.Resolve().GetTypeInfo())
                    || !partsBeingBuilt.Add(part.TypeRef))
                {
                    return false;
                }

                try
                {
                    MethodBase importingConstructorOrFactoryMethod = part.ImportingConstructorOrFactoryMethod!;
                    if (importingConstructorOrFactoryMethod is not ConstructorInfo
                        || importingConstructorOrFactoryMethod.ContainsGenericParameters)
                    {
                        return false;
                    }

                    IReadOnlyList<RuntimeComposition.RuntimeImport> constructorImports = part.ImportingConstructorArguments;
                    var argumentFactories = new Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>[constructorImports.Count];
                    int operationCount = 1;
                    for (int i = 0; i < constructorImports.Count; i++)
                    {
                        RuntimeComposition.RuntimeImport import = constructorImports[i];
                        if (import.IsLazy
                            || import.IsExportFactory
                            || import.IsNonSharedInstanceRequired)
                        {
                            return false;
                        }

                        if (import.Cardinality == ImportCardinality.ZeroOrMore)
                        {
                            Type importingSiteType = import.ImportingSiteType;
                            if (!importingSiteType.IsArray
                                && (!importingSiteType.GetTypeInfo().IsGenericType
                                    || !importingSiteType.GetGenericTypeDefinition().IsEquivalentTo(typeof(IEnumerable<>))))
                            {
                                return false;
                            }

                            Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>[] elementFactories =
                                new Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>[import.SatisfyingExports.Count];
                            int elementIndex = 0;
                            foreach (RuntimeComposition.RuntimeExport importedExport in import.SatisfyingExports)
                            {
                                if (!this.TryCreateDirectImportFactory(
                                    import,
                                    importedExport,
                                    partsBeingBuilt,
                                    out Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>? elementFactory,
                                    out int elementOperationCount))
                                {
                                    return false;
                                }

                                elementFactories[elementIndex++] = elementFactory;
                                operationCount += elementOperationCount;
                            }

                            Type elementType = import.ImportingSiteTypeWithoutCollection;
                            argumentFactories[i] = (exportProvider, importingPartTracker) =>
                            {
                                Array values = Array.CreateInstance(elementType, elementFactories.Length);
                                for (int j = 0; j < elementFactories.Length; j++)
                                {
                                    values.SetValue(elementFactories[j](exportProvider, importingPartTracker), j);
                                }

                                return values;
                            };
                        }
                        else
                        {
                            if (import.SatisfyingExports.Count != 1
                                || !this.TryCreateDirectImportFactory(
                                    import,
                                    import.SatisfyingExports.First(),
                                    partsBeingBuilt,
                                    out Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>? argumentFactory,
                                    out int argumentOperationCount))
                            {
                                return false;
                            }

                            argumentFactories[i] = argumentFactory;
                            operationCount += argumentOperationCount;
                        }
                    }

                    IReadOnlyList<RuntimeComposition.RuntimeImport> memberImports = part.ImportingMembers;
                    var memberAssignments = new DirectMemberImport[memberImports.Count];
                    for (int i = 0; i < memberImports.Count; i++)
                    {
                        RuntimeComposition.RuntimeImport import = memberImports[i];
                        if (import.Cardinality != ImportCardinality.ExactlyOne
                            || import.IsLazy
                            || import.IsExportFactory
                            || import.IsNonSharedInstanceRequired
                            || import.SatisfyingExports.Count != 1
                            || !this.TryCreateDirectImportFactory(
                                import,
                                import.SatisfyingExports.First(),
                                partsBeingBuilt,
                                out Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>? importValueFactory,
                                out int importOperationCount))
                        {
                            return false;
                        }

                        operationCount += importOperationCount + 1;
                        memberAssignments[i] = new DirectMemberImport(
                            import,
                            importValueFactory,
                            this.GetOrCreateImportingMemberSetter(import.ImportingMember!));
                    }

                    activationPlan = new DirectActivationPlan(
                        part,
                        argumentFactories,
                        memberAssignments,
                        this.GetOrCreateInstanceFactory(importingConstructorOrFactoryMethod),
                        operationCount);
                    return true;
                }
                finally
                {
                    partsBeingBuilt.Remove(part.TypeRef);
                }
            }

            internal bool TryCreateFusedActivationPlan(
                RuntimeComposition.RuntimePart part,
                int minimumOperationCount,
                int maximumOperationCount,
                [NotNullWhen(true)] out DirectActivationPlan? activationPlan)
            {
                try
                {
                    return this.TryCreateFusedActivationPlanCore(part, minimumOperationCount, maximumOperationCount, out activationPlan);
                }
                catch (Exception ex) when (ex.IsExpressionCompilationFailure())
                {
                    activationPlan = null;
                    return false;
                }
            }

            private bool TryCreateFusedActivationPlanCore(
                RuntimeComposition.RuntimePart part,
                int minimumOperationCount,
                int maximumOperationCount,
                [NotNullWhen(true)] out DirectActivationPlan? activationPlan)
            {
                activationPlan = null;
                var exportProviderParameter = Expression.Parameter(typeof(RuntimeExportProvider), "exportProvider");
                var importingPartTrackerParameter = Expression.Parameter(typeof(RuntimePartLifecycleTracker), "importingPartTracker");
                int operationCount = 0;
                if (!this.TryCreateFusedPartExpression(
                    part,
                    exportProviderParameter,
                    importingPartTrackerParameter,
                    includeMemberImports: false,
                    allowLifecycleFallback: true,
                    new HashSet<TypeRef>(),
                    ref operationCount,
                    maximumOperationCount,
                    out Expression? creationExpression))
                {
                    return false;
                }

                var instanceParameter = Expression.Parameter(typeof(object), "instance");
                var rootPartsBeingBuilt = new HashSet<TypeRef> { part.TypeRef };
                if (!this.TryCreateFusedMemberAssignments(
                    part,
                    Expression.Convert(instanceParameter, part.TypeRef.Resolve()),
                    exportProviderParameter,
                    importingPartTrackerParameter,
                    rootPartsBeingBuilt,
                    allowLifecycleFallback: true,
                    ref operationCount,
                    maximumOperationCount,
                    out IReadOnlyList<Expression>? memberAssignments)
                    || operationCount < minimumOperationCount)
                {
                    return false;
                }

                Expression<Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>> createValueExpression =
                    Expression.Lambda<Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>>(
                        Expression.Convert(creationExpression, typeof(object)),
                        exportProviderParameter,
                        importingPartTrackerParameter);
                if (!createValueExpression.TryCompile(out Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>? createValue))
                {
                    return false;
                }

                Action<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>? satisfyImports = null;
                if (memberAssignments.Count > 0)
                {
                    var satisfactionExpressions = new Expression[memberAssignments.Count + 1];
                    for (int i = 0; i < memberAssignments.Count; i++)
                    {
                        satisfactionExpressions[i] = memberAssignments[i];
                    }

                    satisfactionExpressions[memberAssignments.Count] = Expression.Empty();
                    Expression<Action<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>> satisfyImportsExpression =
                        Expression.Lambda<Action<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>>(
                            Expression.Block(satisfactionExpressions),
                            exportProviderParameter,
                            importingPartTrackerParameter,
                            instanceParameter);
                    if (!satisfyImportsExpression.TryCompile(out satisfyImports))
                    {
                        return false;
                    }
                }

                this.activationPlanRegistry.RecordFusedExpressionCompilation();
                if (satisfyImports is object)
                {
                    this.activationPlanRegistry.RecordFusedExpressionCompilation();
                }

                activationPlan = new DirectActivationPlan(part, createValue, satisfyImports, operationCount);
                return true;
            }

            private bool TryCreateFusedPartExpression(
                RuntimeComposition.RuntimePart part,
                ParameterExpression exportProviderParameter,
                ParameterExpression importingPartTrackerParameter,
                bool includeMemberImports,
                bool allowLifecycleFallback,
                HashSet<TypeRef> partsBeingBuilt,
                ref int operationCount,
                int maximumOperationCount,
                [NotNullWhen(true)] out Expression? partExpression)
            {
                partExpression = null;
                if (operationCount >= maximumOperationCount
                    || part.SharingBoundary?.Length == 0
                    || !part.IsInstantiable
                    || part.TypeRef.IsGenericTypeDefinition
                    || part.OnImportsSatisfiedMethodRefs.Count > 0
                    || typeof(IDisposable).GetTypeInfo().IsAssignableFrom(part.TypeRef.Resolve().GetTypeInfo())
                    || !partsBeingBuilt.Add(part.TypeRef))
                {
                    return false;
                }

                try
                {
                    if (part.ImportingConstructorOrFactoryMethod is not ConstructorInfo constructor
                        || constructor.ContainsGenericParameters)
                    {
                        return false;
                    }

                    IReadOnlyList<RuntimeComposition.RuntimeImport> constructorImports = part.ImportingConstructorArguments;
                    ParameterInfo[] constructorParameters = constructor.GetParameters();
                    var constructorArguments = new ParameterExpression[constructorImports.Count];
                    var variables = new List<ParameterExpression>(constructorImports.Count + 1);
                    var expressions = new List<Expression>(constructorImports.Count + part.ImportingMembers.Count + 2);
                    for (int i = 0; i < constructorImports.Count; i++)
                    {
                        RuntimeComposition.RuntimeImport import = constructorImports[i];
                        if (!this.TryCreateFusedImportExpression(
                            import,
                            exportProviderParameter,
                            importingPartTrackerParameter,
                            partsBeingBuilt,
                            allowLifecycleFallback,
                            ref operationCount,
                            maximumOperationCount,
                            out Expression? importExpression))
                        {
                            return false;
                        }

                        var argumentVariable = Expression.Variable(constructorParameters[i].ParameterType, "argument" + i);
                        constructorArguments[i] = argumentVariable;
                        variables.Add(argumentVariable);
                        expressions.Add(Expression.Assign(
                            argumentVariable,
                            ConvertFusedImportValue(importExpression, constructorParameters[i].ParameterType)));
                    }

                    operationCount++;
                    if (operationCount > maximumOperationCount)
                    {
                        return false;
                    }

                    Type partType = part.TypeRef.Resolve();
                    var instanceVariable = Expression.Variable(partType, "part");
                    variables.Add(instanceVariable);
                    var activationException = Expression.Parameter(typeof(Exception), "activationException");
                    expressions.Add(Expression.TryCatch(
                        Expression.Assign(instanceVariable, Expression.New(constructor, constructorArguments)),
                        Expression.Catch(
                            activationException,
                            Expression.Throw(
                                Expression.Call(
                                    CreateFusedPartActivationExceptionMethod,
                                    Expression.Constant(part),
                                    activationException),
                                partType))));

                    if (includeMemberImports)
                    {
                        if (!this.TryCreateFusedMemberAssignments(
                            part,
                            instanceVariable,
                            exportProviderParameter,
                            importingPartTrackerParameter,
                            partsBeingBuilt,
                            allowLifecycleFallback,
                            ref operationCount,
                            maximumOperationCount,
                            out IReadOnlyList<Expression>? memberAssignments))
                        {
                            return false;
                        }

                        expressions.AddRange(memberAssignments);
                    }

                    expressions.Add(instanceVariable);
                    partExpression = Expression.Block(variables, expressions);
                    return true;
                }
                finally
                {
                    partsBeingBuilt.Remove(part.TypeRef);
                }
            }

            private bool TryCreateFusedMemberAssignments(
                RuntimeComposition.RuntimePart part,
                Expression instanceExpression,
                ParameterExpression exportProviderParameter,
                ParameterExpression importingPartTrackerParameter,
                HashSet<TypeRef> partsBeingBuilt,
                bool allowLifecycleFallback,
                ref int operationCount,
                int maximumOperationCount,
                [NotNullWhen(true)] out IReadOnlyList<Expression>? memberAssignments)
            {
                IReadOnlyList<RuntimeComposition.RuntimeImport> memberImports = part.ImportingMembers;
                var assignments = new Expression[memberImports.Count];
                for (int i = 0; i < memberImports.Count; i++)
                {
                    RuntimeComposition.RuntimeImport import = memberImports[i];
                    if (import.Cardinality != ImportCardinality.ExactlyOne
                        || (!allowLifecycleFallback && (import.IsLazy || import.IsExportFactory || import.IsNonSharedInstanceRequired))
                        || import.SatisfyingExports.Count != 1
                        || !this.TryCreateFusedImportExpression(
                            import,
                            exportProviderParameter,
                            importingPartTrackerParameter,
                            partsBeingBuilt,
                            allowLifecycleFallback,
                            ref operationCount,
                            maximumOperationCount,
                            out Expression? importedValueExpression))
                    {
                        memberAssignments = null;
                        return false;
                    }

                    MemberInfo importingMember = import.ImportingMember!;
                    Type importingMemberType = importingMember switch
                    {
                        PropertyInfo property => property.PropertyType,
                        FieldInfo field => field.FieldType,
                        _ => throw new NotSupportedException(),
                    };
                    Expression importingMemberExpression = importingMember switch
                    {
                        PropertyInfo property => Expression.Property(instanceExpression, property),
                        FieldInfo field => Expression.Field(instanceExpression, field),
                        _ => throw new NotSupportedException(),
                    };
                    var importedValueVariable = Expression.Variable(importingMemberType, "importedValue" + i);
                    var importException = Expression.Parameter(typeof(CompositionFailedException), "importException");
                    var assignmentException = Expression.Parameter(typeof(Exception), "assignmentException");
                    assignments[i] = Expression.Block(
                        new[] { importedValueVariable },
                        Expression.TryCatch(
                            Expression.Assign(
                                importedValueVariable,
                                ConvertFusedImportValue(importedValueExpression, importingMemberType)),
                            Expression.Catch(
                                importException,
                                Expression.Throw(
                                    Expression.Call(
                                        CreateFusedImportAssignmentExceptionMethod,
                                        Expression.Constant(import),
                                        importException),
                                    importingMemberType))),
                        Expression.TryCatch(
                            Expression.Assign(importingMemberExpression, importedValueVariable),
                            Expression.Catch(
                                assignmentException,
                                Expression.Throw(
                                    Expression.Call(
                                        CreateFusedPartActivationExceptionMethod,
                                        Expression.Constant(part),
                                        assignmentException),
                                    importingMemberType))));

                    operationCount++;
                    if (operationCount > maximumOperationCount)
                    {
                        memberAssignments = null;
                        return false;
                    }
                }

                memberAssignments = assignments;
                return true;
            }

            private bool TryCreateFusedImportExpression(
                RuntimeComposition.RuntimeImport import,
                ParameterExpression exportProviderParameter,
                ParameterExpression importingPartTrackerParameter,
                HashSet<TypeRef> partsBeingBuilt,
                bool allowLifecycleFallback,
                ref int operationCount,
                int maximumOperationCount,
                [NotNullWhen(true)] out Expression? importExpression)
            {
                importExpression = null;
                if (import.IsLazy || import.IsExportFactory || import.IsNonSharedInstanceRequired)
                {
                    if (!allowLifecycleFallback)
                    {
                        return false;
                    }

                    importExpression = Expression.Property(
                        Expression.Call(
                            exportProviderParameter,
                            GetValueForImportSiteMethod,
                            importingPartTrackerParameter,
                            Expression.Constant(import)),
                        nameof(ValueForImportSite.Value));
                    return true;
                }

                if (import.Cardinality == ImportCardinality.ZeroOrMore)
                {
                    Type importingSiteType = import.ImportingSiteType;
                    if (!importingSiteType.IsArray
                        && (!importingSiteType.GetTypeInfo().IsGenericType
                            || !importingSiteType.GetGenericTypeDefinition().IsEquivalentTo(typeof(IEnumerable<>))))
                    {
                        return false;
                    }

                    Type elementType = import.ImportingSiteTypeWithoutCollection;
                    var elementExpressions = new Expression[import.SatisfyingExports.Count];
                    int elementIndex = 0;
                    foreach (RuntimeComposition.RuntimeExport importedExport in import.SatisfyingExports)
                    {
                        if (!this.TryCreateFusedImportElementExpression(
                            import,
                            importedExport,
                            exportProviderParameter,
                            importingPartTrackerParameter,
                            partsBeingBuilt,
                            ref operationCount,
                            maximumOperationCount,
                            out Expression? elementExpression))
                        {
                            return false;
                        }

                        elementExpressions[elementIndex++] = ConvertFusedImportValue(elementExpression, elementType);
                    }

                    importExpression = Expression.NewArrayInit(elementType, elementExpressions);
                    return true;
                }

                return import.SatisfyingExports.Count == 1
                    && this.TryCreateFusedImportElementExpression(
                        import,
                        import.SatisfyingExports.First(),
                        exportProviderParameter,
                        importingPartTrackerParameter,
                        partsBeingBuilt,
                        ref operationCount,
                        maximumOperationCount,
                        out importExpression);
            }

            private bool TryCreateFusedImportElementExpression(
                RuntimeComposition.RuntimeImport import,
                RuntimeComposition.RuntimeExport importedExport,
                ParameterExpression exportProviderParameter,
                ParameterExpression importingPartTrackerParameter,
                HashSet<TypeRef> partsBeingBuilt,
                ref int operationCount,
                int maximumOperationCount,
                [NotNullWhen(true)] out Expression? importExpression)
            {
                importExpression = null;
                if (importedExport.MemberRef is object)
                {
                    return false;
                }

                RuntimeComposition.RuntimePart importedPart = this.composition.GetPart(importedExport);
                if (importedPart.TypeRef.IsGenericTypeDefinition)
                {
                    return false;
                }

                if (importedPart.IsShared)
                {
                    var providerLookup = new ProviderScopedRuntimeExportLookup(import, importedPart, importedExport);
                    MethodInfo getValueMethod = typeof(ProviderScopedRuntimeExportLookup).GetMethod(
                        nameof(ProviderScopedRuntimeExportLookup.GetValue),
                        BindingFlags.Instance | BindingFlags.NonPublic)!;
                    importExpression = Expression.Call(
                        Expression.Constant(providerLookup),
                        getValueMethod,
                        exportProviderParameter,
                        importingPartTrackerParameter);
                    return true;
                }

                return this.TryCreateFusedPartExpression(
                    importedPart,
                    exportProviderParameter,
                    importingPartTrackerParameter,
                    includeMemberImports: true,
                    allowLifecycleFallback: false,
                    partsBeingBuilt,
                    ref operationCount,
                    maximumOperationCount,
                    out importExpression);
            }

            private static Expression ConvertFusedImportValue(Expression valueExpression, Type destinationType)
            {
                return destinationType.GetTypeInfo().IsValueType && Nullable.GetUnderlyingType(destinationType) is null
                    ? Expression.Condition(
                        Expression.ReferenceEqual(Expression.Convert(valueExpression, typeof(object)), Expression.Constant(null)),
                        Expression.Default(destinationType),
                        Expression.Convert(valueExpression, destinationType))
                    : Expression.Convert(valueExpression, destinationType);
            }

            private bool TryCreateDirectImportFactory(
                RuntimeComposition.RuntimeImport import,
                RuntimeComposition.RuntimeExport importedExport,
                HashSet<TypeRef> partsBeingBuilt,
                [NotNullWhen(true)] out Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>? valueFactory,
                out int operationCount)
            {
                valueFactory = null;
                operationCount = 0;
                if (importedExport.MemberRef is object)
                {
                    return false;
                }

                RuntimeComposition.RuntimePart importedPart = this.composition.GetPart(importedExport);
                if (importedPart.TypeRef.IsGenericTypeDefinition)
                {
                    return false;
                }

                if (importedPart.IsShared)
                {
                    var providerLookups = new ProviderScopedRuntimeExportLookup(import, importedPart, importedExport);
                    valueFactory = providerLookups.GetValue;
                    return true;
                }

                if (this.TryCreateDirectActivationPlan(importedPart, partsBeingBuilt, out DirectActivationPlan? activationPlan))
                {
                    valueFactory = activationPlan.Activate;
                    operationCount = activationPlan.OperationCount;
                    return true;
                }

                return false;
            }

            private Lazy<Action<object, object?>> GetOrCreateImportingMemberSetter(MemberInfo member)
            {
                return this.activationPlanRegistry.GetOrCreateLazyImportingMemberSetter(member);
            }

            private Lazy<Func<object?[], object?>> GetOrCreateInstanceFactory(MethodBase method)
            {
                return this.activationPlanRegistry.GetOrCreateLazyInstanceFactory(method);
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
                RuntimeComposition.RuntimePart exportedPart = this.composition.GetPart(export);
                Func<KeyValuePair<object?, IDisposable?>> valueFactory = () =>
                {
                    this.activationPlanRegistry.RecordExportFactoryActivation(exportedPart);
                    RuntimeExportProvider scope = newSharingScope
                        ? new RuntimeExportProvider(this.composition, this.activationPlanRegistry, this, sharingBoundaries)
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

                var constructedType = GetPartConstructedTypeRef(exportingRuntimePart, import.Metadata);

                partLifecycle = this.GetOrCreateValue(import, exportingRuntimePart, exportingRuntimePart.TypeRef, constructedType, importingPartTracker);

                return lazy ? ConstructLazyExportedValue(import, export, importingPartTracker, partLifecycle, this.faultCallback) :
                              ConstructExportedValue(import, export, importingPartTracker, partLifecycle, this.faultCallback);

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

            private static Exception CreateFusedImportAssignmentException(
                RuntimeComposition.RuntimeImport import,
                CompositionFailedException exception)
            {
                return new CompositionFailedException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ErrorWhileSettingImport,
                        RuntimeComposition.GetDiagnosticLocation(import)),
                    exception);
            }

            private static Exception CreateFusedPartActivationException(
                RuntimeComposition.RuntimePart part,
                Exception exception)
            {
                return new CompositionFailedException(
                    Strings.FormatExceptionThrownByPartUnderInitialization(part.TypeRef.Resolve().FullName),
                    exception);
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

            internal sealed class DirectActivationPlan
            {
                private readonly Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>[] argumentFactories;
                private readonly Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>? fusedCreateValue;
                private readonly Action<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>? fusedSatisfyImports;
                private readonly Lazy<Func<object?[], object?>>? instanceFactory;
                private readonly DirectMemberImport[] memberAssignments;
                private readonly RuntimeComposition.RuntimePart part;

                internal DirectActivationPlan(
                    RuntimeComposition.RuntimePart part,
                    Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>[] argumentFactories,
                    DirectMemberImport[] memberAssignments,
                    Lazy<Func<object?[], object?>> instanceFactory,
                    int operationCount)
                {
                    this.part = part;
                    this.argumentFactories = argumentFactories;
                    this.memberAssignments = memberAssignments;
                    this.instanceFactory = instanceFactory;
                    this.OperationCount = operationCount;
                }

                internal DirectActivationPlan(
                    RuntimeComposition.RuntimePart part,
                    Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object> createValue,
                    Action<RuntimeExportProvider, RuntimePartLifecycleTracker?, object>? satisfyImports,
                    int operationCount)
                {
                    this.part = part;
                    this.argumentFactories = Array.Empty<Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?>>();
                    this.memberAssignments = Array.Empty<DirectMemberImport>();
                    this.fusedCreateValue = createValue;
                    this.fusedSatisfyImports = satisfyImports;
                    this.OperationCount = operationCount;
                }

                internal int OperationCount { get; }

                internal object Activate(RuntimeExportProvider exportProvider, RuntimePartLifecycleTracker? importingPartTracker)
                {
                    object instance = this.CreateValue(exportProvider, importingPartTracker);
                    this.SatisfyImports(exportProvider, importingPartTracker, instance);
                    return instance;
                }

                internal object CreateValue(RuntimeExportProvider exportProvider, RuntimePartLifecycleTracker? importingPartTracker)
                {
                    if (this.fusedCreateValue is object)
                    {
                        return this.fusedCreateValue(exportProvider, importingPartTracker);
                    }

                    object?[] arguments = this.argumentFactories.Length == 0 ? EmptyObjectArray : new object?[this.argumentFactories.Length];
                    for (int i = 0; i < this.argumentFactories.Length; i++)
                    {
                        arguments[i] = this.argumentFactories[i](exportProvider, importingPartTracker);
                    }

                    try
                    {
                        object? instance = this.instanceFactory!.Value(arguments);
                        Assumes.NotNull(instance);
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        throw new CompositionFailedException(
                            Strings.FormatExceptionThrownByPartUnderInitialization(this.part.TypeRef.Resolve().FullName),
                            ex);
                    }
                }

                internal void SatisfyImports(RuntimeExportProvider exportProvider, RuntimePartLifecycleTracker? importingPartTracker, object instance)
                {
                    if (this.fusedCreateValue is object)
                    {
                        this.fusedSatisfyImports?.Invoke(exportProvider, importingPartTracker, instance);
                        return;
                    }

                    for (int i = 0; i < this.memberAssignments.Length; i++)
                    {
                        DirectMemberImport assignment = this.memberAssignments[i];
                        object? importedValue;
                        try
                        {
                            importedValue = assignment.ValueFactory(exportProvider, importingPartTracker);
                        }
                        catch (CompositionFailedException ex)
                        {
                            throw new CompositionFailedException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    Strings.ErrorWhileSettingImport,
                                    RuntimeComposition.GetDiagnosticLocation(assignment.Import)),
                                ex);
                        }

                        try
                        {
                            assignment.Setter.Value(instance, importedValue);
                        }
                        catch (Exception ex)
                        {
                            throw new CompositionFailedException(
                                Strings.FormatExceptionThrownByPartUnderInitialization(this.part.TypeRef.Resolve().FullName),
                                ex);
                        }
                    }
                }
            }

            internal readonly struct DirectMemberImport
            {
                internal DirectMemberImport(
                    RuntimeComposition.RuntimeImport import,
                    Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?> valueFactory,
                    Lazy<Action<object, object?>> setter)
                {
                    this.Import = import;
                    this.ValueFactory = valueFactory;
                    this.Setter = setter;
                }

                internal RuntimeComposition.RuntimeImport Import { get; }

                internal Lazy<Action<object, object?>> Setter { get; }

                internal Func<RuntimeExportProvider, RuntimePartLifecycleTracker?, object?> ValueFactory { get; }
            }

            private sealed class ProviderScopedRuntimeExportLookup
            {
                private readonly ConditionalWeakTable<RuntimeExportProvider, RuntimeExportLookup> providerLookups = new();
                private readonly RuntimeComposition.RuntimeExport export;
                private readonly RuntimeComposition.RuntimeImport import;
                private readonly RuntimeComposition.RuntimePart part;
                private FirstProviderLookup? firstProviderLookup;

                internal ProviderScopedRuntimeExportLookup(
                    RuntimeComposition.RuntimeImport import,
                    RuntimeComposition.RuntimePart part,
                    RuntimeComposition.RuntimeExport export)
                {
                    this.import = import;
                    this.part = part;
                    this.export = export;
                }

                internal object? GetValue(RuntimeExportProvider exportProvider, RuntimePartLifecycleTracker? importingPartTracker)
                {
                    RuntimeExportLookup lookup = this.GetLookup(exportProvider);
                    if (lookup.TryGetSharedValue(out object? sharedValue))
                    {
                        return sharedValue;
                    }

                    sharedValue = exportProvider.GetExportedValue(this.import, this.export, importingPartTracker, out PartLifecycleTracker? partLifecycle);
                    if (partLifecycle?.State == PartLifecycleState.Final)
                    {
                        lookup.SetSharedValue(sharedValue);
                    }

                    return sharedValue;
                }

                internal RuntimeExportLookup GetLookup(RuntimeExportProvider exportProvider)
                {
                    FirstProviderLookup? firstLookup = Volatile.Read(ref this.firstProviderLookup);
                    if (firstLookup is null)
                    {
                        RuntimeExportLookup lookup = this.providerLookups.GetValue(
                            exportProvider,
                            _ => new RuntimeExportLookup(this.part, this.export));
                        var newFirstLookup = new FirstProviderLookup(exportProvider, lookup);
                        firstLookup = Interlocked.CompareExchange(ref this.firstProviderLookup, newFirstLookup, null) ?? newFirstLookup;
                    }

                    if (firstLookup.ExportProvider.TryGetTarget(out RuntimeExportProvider? firstProvider)
                        && ReferenceEquals(firstProvider, exportProvider)
                        && firstLookup.Lookup.TryGetTarget(out RuntimeExportLookup? firstProviderRuntimeLookup))
                    {
                        return firstProviderRuntimeLookup;
                    }

                    return this.providerLookups.GetValue(
                        exportProvider,
                        _ => new RuntimeExportLookup(this.part, this.export));
                }

                private sealed class FirstProviderLookup
                {
                    internal FirstProviderLookup(RuntimeExportProvider exportProvider, RuntimeExportLookup lookup)
                    {
                        this.ExportProvider = new WeakReference<RuntimeExportProvider>(exportProvider);
                        this.Lookup = new WeakReference<RuntimeExportLookup>(lookup);
                    }

                    internal WeakReference<RuntimeExportProvider> ExportProvider { get; }

                    internal WeakReference<RuntimeExportLookup> Lookup { get; }
                }
            }

            private sealed class RuntimeExportLookup
            {
                internal static readonly RuntimeExportLookup Unsupported = new RuntimeExportLookup();
                private volatile DirectActivationPlan? directActivationPlan;
                private volatile bool hasSharedValue;
                private object? sharedValue;

                private RuntimeExportLookup()
                {
                }

                internal RuntimeExportLookup(RuntimeComposition.RuntimePart part, RuntimeComposition.RuntimeExport export)
                {
                    this.Part = part;
                    this.Export = export;
                }

                internal bool CanUseFastPath => this.Part is object;

                internal RuntimeComposition.RuntimePart? Part { get; }

                internal RuntimeComposition.RuntimeExport? Export { get; }

                internal bool TryGetSharedValue(out object? value)
                {
                    if (this.hasSharedValue)
                    {
                        value = this.sharedValue;
                        return true;
                    }

                    value = null;
                    return false;
                }

                internal void SetSharedValue(object? value)
                {
                    if (this.Part!.IsShared && this.Export!.MemberRef is null)
                    {
                        this.sharedValue = value;
                        this.hasSharedValue = true;
                    }
                }

                internal bool SetDirectActivationPlan(DirectActivationPlan activationPlan)
                {
                    this.directActivationPlan = activationPlan;
                    return true;
                }

                internal bool TryGetDirectActivationPlan([NotNullWhen(true)] out DirectActivationPlan? activationPlan)
                {
                    activationPlan = this.directActivationPlan;
                    return activationPlan is object;
                }
            }

            [DebuggerDisplay("{" + nameof(partDefinition) + "." + nameof(RuntimeComposition.RuntimePart.TypeRef) + "." + nameof(TypeRef.ResolvedType) + ".FullName,nq} ({State})")]
            internal class RuntimePartLifecycleTracker : PartLifecycleTracker
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

                protected override bool CanInitializeNonSharedValueDirectly =>
                    this.partDefinition.IsInstantiable
                    && this.partDefinition.ImportingConstructorArguments.Count == 0
                    && this.partDefinition.ImportingMembers.Count == 0
                    && this.partDefinition.OnImportsSatisfiedMethodRefs.Count == 0;

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
                    IReadOnlyList<RuntimeComposition.RuntimeImport> constructorImports = this.partDefinition.ImportingConstructorArguments;
                    object?[] ctorArgs = constructorImports.Count == 0 ? EmptyObjectArray : new object?[constructorImports.Count];
                    for (int i = 0; i < constructorImports.Count; i++)
                    {
                        ctorArgs[i] = this.OwningExportProvider.GetValueForImportSite(this, constructorImports[i]).Value;
                    }

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
                        object? part = this.OwningExportProvider.activationPlanRegistry.TryGetInstanceFactory(
                            this.partDefinition,
                            importingConstructorOrFactoryMethod,
                            out Func<object?[], object?>? instanceFactory)
                            ? instanceFactory!(ctorArgs)
                            : importingConstructorOrFactoryMethod.Instantiate(ctorArgs);
                        Assumes.NotNull(part);
                        return part;
                    }
                    catch (Exception ex)
                    {
                        throw this.PrepareExceptionForFaultedPart(ex as TargetInvocationException ?? new TargetInvocationException(ex));
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

            private sealed class DirectActivationRuntimePartLifecycleTracker : RuntimePartLifecycleTracker
            {
                private readonly DirectActivationPlan activationPlan;

                internal DirectActivationRuntimePartLifecycleTracker(
                    RuntimeExportProvider owningExportProvider,
                    RuntimeComposition.RuntimePart partDefinition,
                    IReadOnlyDictionary<string, object?> importMetadata,
                    DirectActivationPlan activationPlan)
                    : base(owningExportProvider, partDefinition, importMetadata)
                {
                    this.activationPlan = activationPlan;
                }

                internal DirectActivationRuntimePartLifecycleTracker(
                    RuntimeExportProvider owningExportProvider,
                    RuntimeComposition.RuntimePart partDefinition,
                    IReadOnlyDictionary<string, object?> importMetadata,
                    PartLifecycleTracker nonSharedPartOwner,
                    DirectActivationPlan activationPlan)
                    : base(owningExportProvider, partDefinition, importMetadata, nonSharedPartOwner)
                {
                    this.activationPlan = activationPlan;
                }

                protected override object CreateValue()
                {
                    return this.activationPlan.CreateValue(this.OwningExportProvider, this);
                }

                protected override void SatisfyImports()
                {
                    this.activationPlan.SatisfyImports(this.OwningExportProvider, this, this.Value!);
                }
            }
        }
    }
}
