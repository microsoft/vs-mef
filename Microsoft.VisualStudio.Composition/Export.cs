namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class Export
    {
        public Export(ExportDefinition exportDefinition, ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");
            Requires.NotNull(partDefinition, "partDefinition");
            this.ExportDefinition = exportDefinition;
            this.PartDefinition = partDefinition;
        }

        public ExportDefinition ExportDefinition { get; private set; }

        public ComposablePartDefinition PartDefinition { get; private set; }
    }
}
