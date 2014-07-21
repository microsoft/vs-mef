namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    [Export]
    public class PartThatLazyImportsExportWithTypeMetadataViaTMetadata
    {
        [Import("AnExportWithMetadataTypeValue")]
        public Lazy<object, IMetadataView> ImportWithTMetadata { get; set; }
    }

    public interface IMetadataView
    {
        Type SomeType { get; }

        [DefaultValue("default")]
        string SomeProperty { get; }
    }
}
