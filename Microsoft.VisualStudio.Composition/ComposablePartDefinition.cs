namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ComposablePartDefinition
    {
        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exports, IReadOnlyDictionary<MemberInfo, ImportDefinition> imports)
        {
            Requires.NotNull(partType, "partType");
            Requires.NotNull(exports, "exports");
            Requires.NotNull(imports, "imports");

            this.Type = partType;
            this.ExportDefinitions = exports;
            this.ImportDefinitions = imports;
        }

        public Type Type { get; private set; }

        public string Id
        {
            get { return this.Type.Name; }
        }

        public IReadOnlyCollection<ExportDefinition> ExportDefinitions { get; private set; }

        public IReadOnlyDictionary<MemberInfo, ImportDefinition> ImportDefinitions { get; private set; }
    }
}
