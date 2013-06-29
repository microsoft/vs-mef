namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ExportDefinition
    {
        public ExportDefinition(CompositionContract contract)
        {
            Requires.NotNull(contract, "contract");
            this.Contract = contract;
        }

        public CompositionContract Contract { get; private set; }
    }
}
