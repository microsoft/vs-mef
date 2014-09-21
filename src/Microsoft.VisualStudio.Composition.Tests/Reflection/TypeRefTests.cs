namespace Microsoft.VisualStudio.Composition.Tests.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class TypeRefTests
    {
        [Fact]
        public void EqualsDistinguishesArrays()
        {
            Assert.NotEqual(TypeRef.Get(typeof(object)), TypeRef.Get(typeof(object[])));
        }
    }
}
