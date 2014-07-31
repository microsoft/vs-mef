namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ExportDefinitionBinding
    {
        public ExportDefinitionBinding(ExportDefinition exportDefinition, ComposablePartDefinition partDefinition, MemberInfo exportingMember)
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

        /// <summary>
        /// Gets a value indicating whether the exporting member is static.
        /// </summary>
        public bool IsStaticExport
        {
            get { return this.ExportingMember.IsStatic(); }
        }

        public Type ExportedValueType
        {
            get { return ReflectionHelpers.GetExportedValueType(this.PartDefinition.Type, this.ExportingMember); }
        }

        internal ExportDefinitionBinding CloseGenericExport(Type[] genericTypeArguments)
        {
            Requires.NotNull(genericTypeArguments, "genericTypeArguments");

            string exportTypeIdentity = string.Format(
                CultureInfo.InvariantCulture,
                (string)this.ExportDefinition.Metadata[CompositionConstants.ExportTypeIdentityMetadataName],
                genericTypeArguments.Select(ContractNameServices.GetTypeIdentity).ToArray());
            var updatedMetadata = ImmutableDictionary.CreateRange(this.ExportDefinition.Metadata)
                .SetItem(CompositionConstants.ExportTypeIdentityMetadataName, exportTypeIdentity);
            return new ExportDefinitionBinding(
                new ExportDefinition(this.ExportDefinition.ContractName, updatedMetadata),
                this.PartDefinition,
                this.ExportingMember);
        }
    }
}
