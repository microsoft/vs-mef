namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    using System;
    using System.Composition;
#if DESKTOP
    using MefV1 = System.ComponentModel.Composition;
#endif

    [MetadataAttribute]
#if DESKTOP
    [MefV1.MetadataAttribute]
#endif
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
