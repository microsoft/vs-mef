namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class Export
    {
        public Export(ExportDefinition exportDefinition, ComposablePartDefinition partDefinition, MemberInfo exportingMember)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");
            Requires.NotNull(partDefinition, "partDefinition");

            this.ExportDefinition = exportDefinition;
            this.PartDefinition = partDefinition;
            this.ExportingMember = exportingMember;
        }

        public ExportDefinition ExportDefinition { get; private set; }

        public ComposablePartDefinition PartDefinition { get; private set; }

        /// <summary>
        /// Gets the member with the ExportAttribute applied. <c>null</c> when the export is on the type itself.
        /// </summary>
        public MemberInfo ExportingMember { get; private set; }
    }
}
