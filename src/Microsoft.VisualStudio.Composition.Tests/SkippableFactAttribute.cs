namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using Xunit;
    using Xunit.Sdk;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Microsoft.VisualStudio.Composition.Tests.SkippableFactDiscoverer", "Microsoft.VisualStudio.Composition.Tests")]
    public class SkippableFactAttribute : FactAttribute
    {
    }
}
