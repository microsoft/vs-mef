namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Export]
    public class ExternalExportWithLazy
    {
        [Import("YetAnotherExport")]
        public Lazy<object> YetAnotherExport { get; set; } = null!;
    }
}
