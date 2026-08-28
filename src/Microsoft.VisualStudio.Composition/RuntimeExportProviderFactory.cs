// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal partial class RuntimeExportProviderFactory : IFaultReportingExportProviderFactory
    {
        private const int ActivationExpressionCompilationThreshold = 2;

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
            private int expressionCompilationCount;

            internal ActivationPlanRegistry(ExportProviderFactoryOptions options)
            {
                this.IsExpressionCompilationEnabled = (options & ExportProviderFactoryOptions.EnableActivationExpressionCompilation) != 0;
            }

            internal int ExpressionCompilationCount => Volatile.Read(ref this.expressionCompilationCount);

            internal bool IsExpressionCompilationEnabled { get; }

            internal Action<object, object?> GetOrCreateImportingMemberSetter(MemberInfo member)
            {
                return this.importingMemberSetters.GetOrAdd(
                    member,
                    member => new Lazy<Action<object, object?>>(
                        () =>
                        {
                            Interlocked.Increment(ref this.expressionCompilationCount);
                            return member.CreateImportingMemberSetter();
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            }

            internal Func<object?[], object?> GetOrCreateInstanceFactory(MethodBase method)
            {
                return this.instanceFactories.GetOrAdd(
                    method,
                    method => new Lazy<Func<object?[], object?>>(
                        () =>
                        {
                            Interlocked.Increment(ref this.expressionCompilationCount);
                            return method.CreateInstanceFactory();
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication)).Value;
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
