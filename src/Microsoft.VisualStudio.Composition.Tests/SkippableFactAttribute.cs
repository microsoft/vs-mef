namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Microsoft.VisualStudio.Composition.Tests.MefFactDiscoverer", "Microsoft.VisualStudio.Composition.Tests")]
    public class SkippableFactAttribute : FactAttribute
    {
    }
}
