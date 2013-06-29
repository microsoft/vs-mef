namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class ImportDefinition
    {
        public bool AllowDefault { get; private set; }

        public CompositionContract Contract { get; private set; }
    }
}
