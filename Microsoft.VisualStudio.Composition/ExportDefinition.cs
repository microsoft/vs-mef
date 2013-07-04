namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ExportDefinition : IEquatable<ExportDefinition>
    {
        public ExportDefinition(CompositionContract contract)
        {
            Requires.NotNull(contract, "contract");
            this.Contract = contract;
        }

        public CompositionContract Contract { get; private set; }

        public string SharingBoundary { get; private set; }

        public bool IsShared
        {
            get { return this.SharingBoundary != null; }
        }

        public IReadOnlyDictionary<string, string> Metadata { get; private set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ExportDefinition);
        }

        public override int GetHashCode()
        {
            return this.Contract == null ? 0 : this.Contract.GetHashCode();
        }

        public bool Equals(ExportDefinition other)
        {
            return this.Contract.Equals(other.Contract)
                && this.SharingBoundary == other.SharingBoundary;
        }
    }
}
