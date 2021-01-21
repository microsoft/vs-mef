// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    public class ExportedDelegate
    {
        private readonly object? target;
        private readonly MethodInfo method;

        public ExportedDelegate(object? target, MethodInfo method)
        {
            Requires.NotNull(method, nameof(method));

            this.target = target;
            this.method = method;
        }

        public Delegate? CreateDelegate(Type delegateType)
        {
            Requires.NotNull(delegateType, nameof(delegateType));

            if (delegateType == typeof(Delegate) || delegateType == typeof(MulticastDelegate))
            {
                delegateType = ReflectionHelpers.GetContractTypeForDelegate(this.method);
            }

            try
            {
                return this.method.CreateDelegate(delegateType, this.target);
            }
            catch (ArgumentException)
            {
                // Bind failure occurs return null;
                return null;
            }
        }
    }
}
