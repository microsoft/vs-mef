// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Diagnostics;

    public abstract partial class ExportProvider
    {
        [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
        private class NonSharedExport : Export, IDisposable
        {
            private IDisposable? partLifecycleTracker;

            internal NonSharedExport(ExportDefinition definition, Func<(object? Value, IDisposable? NonSharedDisposalTracker)> exportedValueGetter)
                : base(definition, () => exportedValueGetter())
            {
            }

            private string DebuggerDisplay => this.Definition.ContractName;

            public void Dispose() => this.partLifecycleTracker?.Dispose();

            internal override object? ValueFilter(object? lazyValue)
            {
                var tuple = ((object? Value, IDisposable NonSharedDisposalTracker))lazyValue!;
                this.partLifecycleTracker = tuple.NonSharedDisposalTracker;
                return tuple.Value;
            }
        }
    }
}
