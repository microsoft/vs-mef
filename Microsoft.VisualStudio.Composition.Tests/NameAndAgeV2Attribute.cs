namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property , AllowMultiple = false, Inherited = true)]
    public class NameAndAgeV2Attribute : Attribute
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
