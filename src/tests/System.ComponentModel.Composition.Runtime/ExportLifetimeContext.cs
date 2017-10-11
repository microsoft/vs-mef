// Copyright (c) Microsoft. All rights reserved.

#if NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ExportLifetimeContext<>))]

#elif NETSTANDARD1_0

namespace System.ComponentModel.Composition
{
    public sealed class ExportLifetimeContext<T> : IDisposable
    {
        private readonly T value;
        private readonly Action disposeAction;

        public ExportLifetimeContext(T value, Action disposeAction)
        {
            this.value = value;
            this.disposeAction = disposeAction;
        }

        public T Value
        {
            get
            {
                return this.value;
            }
        }

        public void Dispose()
        {
            if (this.disposeAction != null)
            {
                this.disposeAction.Invoke();
            }
        }
    }
}

#endif
