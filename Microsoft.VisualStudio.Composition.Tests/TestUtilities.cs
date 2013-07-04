namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class TestUtilities
    {
        internal static CompositionContainer CreateContainer(params Type[] parts)
        {
            return CompositionConfiguration.Create(parts).CreateContainer();
        }
    }
}
