namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Export]
    public class PartThatLazyImportsExportWithTypeMetadataViaDictionary
    {
        [Import("AnExportWithMetadataTypeValue")]
        public Lazy<object, IDictionary<string, object>> ImportWithDictionary { get; set; } = null!;
    }
}
