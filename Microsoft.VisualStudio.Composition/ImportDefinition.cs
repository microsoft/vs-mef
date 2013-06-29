namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ImportDefinition
    {
        public ImportDefinition(CompositionContract contract, bool allowDefault)
        {
            Requires.NotNull(contract, "contract");

            this.Contract = contract;
            this.AllowDefault = allowDefault;
        }

        public bool AllowDefault { get; private set; }

        public CompositionContract Contract { get; private set; }
    }
}
