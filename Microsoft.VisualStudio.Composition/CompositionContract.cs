namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class CompositionContract
    {
        public CompositionContract(string contractName, Type type)
        {
            Requires.NotNull(type, "type");

            this.ContractName = contractName;
            this.Type = type;
        }

        public string ContractName { get; private set; }

        public Type Type { get; private set; }
    }
}
