// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal partial class RuntimeExportProviderFactory : IFaultReportingExportProviderFactory
    {
        private const int ActivationExpressionCompilationThreshold = 2;
        private const int NamedBoundaryDirectActivationPlanMinimumOperationCount = 4;
        private const int FusedActivationPlanMaximumOperationCount = 64;

        private readonly ActivationPlanRegistry activationPlanRegistry;
        private readonly RuntimeComposition composition;

        internal RuntimeExportProviderFactory(RuntimeComposition composition)
            : this(composition, ExportProviderFactoryOptions.None)
        {
        }

        internal RuntimeExportProviderFactory(RuntimeComposition composition, ExportProviderFactoryOptions options)
        {
            Requires.NotNull(composition, nameof(composition));
            this.composition = composition;
            this.activationPlanRegistry = new ActivationPlanRegistry(options);
        }

        internal int ExpressionCompilationCount => this.activationPlanRegistry.ExpressionCompilationCount;

        internal int DirectActivationPlanCount => this.activationPlanRegistry.DirectActivationPlanCount;

        internal int FusedActivationPlanCount => this.activationPlanRegistry.FusedActivationPlanCount;

        internal int FusedExpressionCompilationCount => this.activationPlanRegistry.FusedExpressionCompilationCount;

        public ExportProvider CreateExportProvider()
        {
            return new RuntimeExportProvider(this.composition, this.activationPlanRegistry);
        }

        public ExportProvider CreateExportProvider(ReportFaultCallback faultCallback)
        {
            Requires.NotNull(faultCallback, nameof(faultCallback));
            return new RuntimeExportProvider(this.composition, this.activationPlanRegistry, faultCallback);
        }

        private sealed class ActivationPlanRegistry
        {
            private readonly ConcurrentDictionary<MethodBase, Lazy<Func<object?[], object?>>> instanceFactories = new();
            private readonly ConcurrentDictionary<MethodBase, int> instanceFactoryActivationCounts = new();
            private readonly ConcurrentDictionary<MemberInfo, Lazy<Action<object, object?>>> importingMemberSetters = new();
            private readonly ConcurrentDictionary<RuntimeComposition.RuntimePart, Lazy<RuntimeExportProvider.DirectActivationPlan?>> directActivationPlans = new();
            private readonly ConcurrentDictionary<RuntimeComposition.RuntimePart, int> directActivationPlanCounts = new();
            private readonly ConcurrentDictionary<RuntimeComposition.RuntimePart, int> exportFactoryActivationCounts = new();
            private readonly ConcurrentDictionary<RuntimeComposition.RuntimePart, Lazy<RuntimeExportProvider.DirectActivationPlan?>> fusedActivationPlans = new();
            private int directActivationPlanCount;
            private int expressionCompilationCount;
            private int fusedActivationPlanCount;
            private int fusedExpressionCompilationCount;

            internal ActivationPlanRegistry(ExportProviderFactoryOptions options)
            {
                this.IsExpressionCompilationEnabled = (options & ExportProviderFactoryOptions.EnableActivationExpressionCompilation) != 0;
            }

            internal int ExpressionCompilationCount => Volatile.Read(ref this.expressionCompilationCount);

            internal int DirectActivationPlanCount => Volatile.Read(ref this.directActivationPlanCount);

            internal int FusedActivationPlanCount => Volatile.Read(ref this.fusedActivationPlanCount);

            internal int FusedExpressionCompilationCount => Volatile.Read(ref this.fusedExpressionCompilationCount);

            internal bool IsExpressionCompilationEnabled { get; }

            internal void RecordExportFactoryActivation(RuntimeComposition.RuntimePart part)
            {
                if (this.IsExpressionCompilationEnabled && IsEligibleForRepeatedActivation(part))
                {
                    this.exportFactoryActivationCounts.AddOrUpdate(
                        part,
                        1,
                        static (_, count) => count < ActivationExpressionCompilationThreshold ? count + 1 : count);
                }
            }

            internal bool TryGetDirectActivationPlan(
                RuntimeExportProvider exportProvider,
                RuntimeComposition.RuntimePart part,
                [NotNullWhen(true)] out RuntimeExportProvider.DirectActivationPlan? activationPlan)
            {
                activationPlan = null;
                if (!this.IsExpressionCompilationEnabled || !IsEligibleForRepeatedActivation(part))
                {
                    return false;
                }

                if (this.exportFactoryActivationCounts.TryGetValue(part, out int factoryActivationCount)
                    && factoryActivationCount >= ActivationExpressionCompilationThreshold
                    && this.TryGetFusedActivationPlan(exportProvider, part, out activationPlan))
                {
                    return true;
                }

                if (this.TryGetExistingDirectActivationPlan(part, out activationPlan))
                {
                    return true;
                }

                int activationCount = this.directActivationPlanCounts.AddOrUpdate(
                    part,
                    1,
                    static (_, count) => count < ActivationExpressionCompilationThreshold ? count + 1 : count);
                if (activationCount < ActivationExpressionCompilationThreshold)
                {
                    return false;
                }

                Lazy<RuntimeExportProvider.DirectActivationPlan?> lazyPlan = this.directActivationPlans.GetOrAdd(
                    part,
                    part => new Lazy<RuntimeExportProvider.DirectActivationPlan?>(
                        () =>
                        {
                            if (exportProvider.TryCreateDirectActivationPlan(part, new HashSet<TypeRef>(), out RuntimeExportProvider.DirectActivationPlan? plan))
                            {
                                if (part.SharingBoundary?.Length > 0
                                    && plan.OperationCount < NamedBoundaryDirectActivationPlanMinimumOperationCount)
                                {
                                    return null;
                                }

                                Interlocked.Increment(ref this.directActivationPlanCount);
                                return plan;
                            }

                            return null;
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));
                activationPlan = lazyPlan.Value;
                return activationPlan is object;
            }

            internal bool TryGetExistingDirectActivationPlan(
                RuntimeComposition.RuntimePart part,
                [NotNullWhen(true)] out RuntimeExportProvider.DirectActivationPlan? activationPlan)
            {
                if (this.directActivationPlans.TryGetValue(part, out Lazy<RuntimeExportProvider.DirectActivationPlan?>? lazyPlan))
                {
                    activationPlan = lazyPlan.Value;
                    return activationPlan is object;
                }

                activationPlan = null;
                return false;
            }

            private bool TryGetFusedActivationPlan(
                RuntimeExportProvider exportProvider,
                RuntimeComposition.RuntimePart part,
                [NotNullWhen(true)] out RuntimeExportProvider.DirectActivationPlan? activationPlan)
            {
                Lazy<RuntimeExportProvider.DirectActivationPlan?> lazyPlan = this.fusedActivationPlans.GetOrAdd(
                    part,
                    part => new Lazy<RuntimeExportProvider.DirectActivationPlan?>(
                        () =>
                        {
                            if (exportProvider.TryCreateFusedActivationPlan(
                                part,
                                NamedBoundaryDirectActivationPlanMinimumOperationCount,
                                FusedActivationPlanMaximumOperationCount,
                                out RuntimeExportProvider.DirectActivationPlan? plan))
                            {
                                Interlocked.Increment(ref this.directActivationPlanCount);
                                Interlocked.Increment(ref this.fusedActivationPlanCount);
                                return plan;
                            }

                            return null;
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));
                activationPlan = lazyPlan.Value;
                return activationPlan is object;
            }

            internal void RecordFusedExpressionCompilation()
            {
                Interlocked.Increment(ref this.expressionCompilationCount);
                Interlocked.Increment(ref this.fusedExpressionCompilationCount);
            }

            internal Action<object, object?> GetOrCreateImportingMemberSetter(MemberInfo member)
                => this.GetOrCreateLazyImportingMemberSetter(member).Value;

            internal Lazy<Action<object, object?>> GetOrCreateLazyImportingMemberSetter(MemberInfo member)
            {
                return this.importingMemberSetters.GetOrAdd(
                    member,
                    member => new Lazy<Action<object, object?>>(
                        () =>
                        {
                            Interlocked.Increment(ref this.expressionCompilationCount);
                            return member.CreateImportingMemberSetter();
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));
            }

            internal Func<object?[], object?> GetOrCreateInstanceFactory(MethodBase method)
                => this.GetOrCreateLazyInstanceFactory(method).Value;

            internal Lazy<Func<object?[], object?>> GetOrCreateLazyInstanceFactory(MethodBase method)
            {
                return this.instanceFactories.GetOrAdd(
                    method,
                    method => new Lazy<Func<object?[], object?>>(
                        () =>
                        {
                            Interlocked.Increment(ref this.expressionCompilationCount);
                            return method.CreateInstanceFactory();
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));
            }

            internal bool TryGetInstanceFactory(RuntimeComposition.RuntimePart part, MethodBase method, out Func<object?[], object?>? factory)
            {
                factory = null;
                if (!this.IsExpressionCompilationEnabled || !IsEligibleForRepeatedActivation(part))
                {
                    return false;
                }

                if (this.instanceFactories.TryGetValue(method, out Lazy<Func<object?[], object?>>? existingFactory))
                {
                    factory = existingFactory.Value;
                    return true;
                }

                int activationCount = this.instanceFactoryActivationCounts.AddOrUpdate(
                    method,
                    1,
                    static (_, count) => count < ActivationExpressionCompilationThreshold ? count + 1 : count);
                if (activationCount < ActivationExpressionCompilationThreshold)
                {
                    return false;
                }

                factory = this.GetOrCreateInstanceFactory(method);
                return true;
            }

            private static bool IsEligibleForRepeatedActivation(RuntimeComposition.RuntimePart part)
            {
                return part.SharingBoundary is null || part.SharingBoundary.Length > 0;
            }
        }
    }
}
