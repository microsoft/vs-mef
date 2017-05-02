// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using Xunit;
    using Xunit.Sdk;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Xunit.Sdk.FactDiscoverer", "xunit.execution.{Platform}")]
    public class RuntimeSpecificFactAttribute : FactAttribute
    {
        public RuntimeSpecificFactAttribute(bool skipOnMono)
        {
            if (skipOnMono && Type.GetType("Mono.Runtime") != null)
            {
                this.Skip = "Test marked as skipped on Mono runtime";
            }
        }
    }
}
