// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;

    internal class DisposableLazy<T> : Lazy<T>, IDisposable
    {
        private readonly Action disposeAction;

        internal DisposableLazy(Func<T> valueFactory, Action disposeAction)
            : base(valueFactory)
        {
            Requires.NotNull(disposeAction, nameof(disposeAction));
            this.disposeAction = disposeAction;
        }

        public void Dispose() => this.disposeAction();
    }
}
