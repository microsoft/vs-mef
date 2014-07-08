namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class Export
    {
        private readonly Func<object> exportedValueGetter;

        public Export(string contractName, IReadOnlyDictionary<string, object> metadata, Func<object> exportedValueGetter)
            : this(new ExportDefinition(contractName, metadata), exportedValueGetter)
        {
        }

        public Export(ExportDefinition definition, Func<object> exportedValueGetter)
        {
            Requires.NotNull(definition, "definition");
            Requires.NotNull(exportedValueGetter, "exportedValueGetter");

            this.Definition = definition;
            this.exportedValueGetter = exportedValueGetter;
        }

        public ExportDefinition Definition { get; private set; }

        /// <summary>
        /// Gets the metadata on the exported value.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata
        {
            get { return this.Definition.Metadata; }
        }

        /// <summary>
        /// Gets the exported value.
        /// </summary>
        /// <remarks>
        /// This may incur a value construction cost upon first retrieval.
        /// </remarks>
        public object Value
        {
            get { return this.exportedValueGetter(); }
        }

        internal Export CloseGenericExport(Type[] genericTypeArguments)
        {
            Requires.NotNull(genericTypeArguments, "genericTypeArguments");

            string openGenericExportTypeIdentity = (string)this.Metadata[CompositionConstants.ExportTypeIdentityMetadataName];
            string genericTypeDefinitionIdentityPattern = openGenericExportTypeIdentity;
            string[] genericTypeArgumentIdentities = genericTypeArguments.Select(ContractNameServices.GetTypeIdentity).ToArray();
            string closedTypeIdentity = string.Format(CultureInfo.InvariantCulture, genericTypeDefinitionIdentityPattern, genericTypeArgumentIdentities);
            var metadata = ImmutableDictionary.CreateRange(this.Metadata).SetItem(CompositionConstants.ExportTypeIdentityMetadataName, closedTypeIdentity);

            string contractName = this.Definition.ContractName == openGenericExportTypeIdentity
                ? closedTypeIdentity : this.Definition.ContractName;

            return new Export(contractName, metadata, this.exportedValueGetter);
        }
    }
}
