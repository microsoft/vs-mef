// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
