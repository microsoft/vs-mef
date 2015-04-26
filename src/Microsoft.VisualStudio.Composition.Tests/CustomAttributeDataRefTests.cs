namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class CustomAttributeDataRefTests
    {
        [Fact, MyCustom("test", Property = 5, Field = 3)]
        public void InstantiateFromCustomAttributeDataCtor()
        {
            foreach (var attrData in MethodBase.GetCurrentMethod().GetCustomAttributesData())
            {
                var info = new CustomAttributeDataRef(attrData);
                Attribute attr = info.Instantiate();
                if (attr is FactAttribute)
                {
                    var fact = (FactAttribute)attr;
                    Assert.Null(fact.DisplayName);
                }
                else if (attr is MyCustomAttribute)
                {
                    var custom = (MyCustomAttribute)attr;
                    Assert.Equal("test", custom.PositionalString);
                    Assert.Equal(5, custom.Property);
                    Assert.Equal(3, custom.Field);
                }
                else
                {
                    Assert.True(false);
                }
            }
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
        private sealed class MyCustomAttribute : Attribute
        {
            public MyCustomAttribute(string positionalString)
            {
                this.PositionalString = positionalString;
            }

            public string PositionalString { get; private set; }

            public int Property { get; set; }

            public int Field { get; set; }
        }
    }
}
