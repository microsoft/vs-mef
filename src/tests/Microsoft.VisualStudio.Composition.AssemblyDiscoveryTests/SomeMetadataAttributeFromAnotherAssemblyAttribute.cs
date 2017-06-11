namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    using System;
    using System.Composition;
    using MefV1 = System.ComponentModel.Composition;

    [MetadataAttribute]
    [MefV1.MetadataAttribute]
    [AttributeUsage(AttributeTargets.All)]
    public class SomeMetadataAttributeFromAnotherAssemblyAttribute : Attribute
    {
        public string SomeProperty { get; }

        public SomeMetadataAttributeFromAnotherAssemblyAttribute(string somePropertyValue)
        {
            this.SomeProperty = somePropertyValue;
        }
    }
}
