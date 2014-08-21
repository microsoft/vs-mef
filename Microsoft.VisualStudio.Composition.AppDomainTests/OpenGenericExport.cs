namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;

    [Export]
    public class OpenGenericExport<T>
    {
    }

    [Export]
    public class PartImportingOpenGenericExport
    {
        [Import]
        public OpenGenericExport<SomeOtherType> ImportingProperty { get; set; }
    }
}
