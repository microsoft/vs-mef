// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class DisposableWithAction : IDisposable
    {
        private readonly Action action;

        internal DisposableWithAction(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            if (this.action != null)
            {
                this.action();
            }
        }
    }
}
