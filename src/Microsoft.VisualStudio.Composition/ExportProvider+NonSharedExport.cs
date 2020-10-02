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
            private static readonly Lazy<object> DummyLazy = new Lazy<object>(() => null);
            private readonly Lazy<object> exportedValueGetter;
            private IDisposable partLifecycleTracker;

            internal NonSharedExport(ExportDefinition definition, Func<(object Value, IDisposable NonSharedDisposalTracker)> exportedValueGetter, ExportProvider owner)
                : base(definition, DummyLazy)
            {
                Requires.NotNull(owner, nameof(owner));
                this.exportedValueGetter = new Lazy<object>(delegate
                {
                    var tuple = exportedValueGetter();
                    this.partLifecycleTracker = tuple.NonSharedDisposalTracker;
                    return tuple.Value;
                });
            }

            public override object Value => this.exportedValueGetter.Value;

            private string DebuggerDisplay => this.Definition.ContractName;

            public void Dispose() => this.partLifecycleTracker.Dispose();
        }
    }
}
