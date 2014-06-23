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

        public IReadOnlyDictionary<string, object> Metadata
        {
            get { return this.Definition.Metadata; }
        }

        public object Value
        {
            get { return this.exportedValueGetter(); }
        }
    }
}
