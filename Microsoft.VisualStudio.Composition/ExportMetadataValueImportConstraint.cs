namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ExportMetadataValueImportConstraint : IImportSatisfiabilityConstraint, IDescriptiveToString
    {
        public ExportMetadataValueImportConstraint(string name, object value)
        {
            Requires.NotNullOrEmpty(name, "name");

            this.Name = name;
            this.Value = value;
        }

        public string Name { get; private set; }

        public object Value { get; private set; }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");

            object exportMetadataValue;
            if (exportDefinition.Metadata.TryGetValue(this.Name, out exportMetadataValue))
            {
                if (EqualityComparer<object>.Default.Equals(this.Value, exportMetadataValue))
                {
                    return true;
                }
            }

            return false;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            indentingWriter.WriteLine("{0} = {1}", this.Name, this.Value);
        }
    }
}
