namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ExportMetadataValueImportConstraint : IImportSatisfiabilityConstraint
    {
        private readonly string name;
        private readonly object value;

        public ExportMetadataValueImportConstraint(string name, object value)
        {
            Requires.NotNullOrEmpty(name, "name");

            this.name = name;
            this.value = value;
        }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");

            object exportMetadataValue;
            if (exportDefinition.Metadata.TryGetValue(this.name, out exportMetadataValue))
            {
                if (EqualityComparer<object>.Default.Equals(this.value, exportMetadataValue))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
