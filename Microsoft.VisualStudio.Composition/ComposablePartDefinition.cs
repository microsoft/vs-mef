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
        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exportsOnType, IReadOnlyDictionary<MemberInfo, ExportDefinition> exportsOnMembers, IReadOnlyDictionary<MemberInfo, ImportDefinition> imports)
        {
            Requires.NotNull(partType, "partType");
            Requires.NotNull(exportsOnType, "exportsOnType");
            Requires.NotNull(exportsOnMembers, "exportsOnMembers");
            Requires.NotNull(imports, "imports");

            this.Type = partType;
            this.ExportDefinitionsOnType = exportsOnType;
            this.ExportDefinitionsOnMembers = exportsOnMembers;
            this.ImportDefinitions = imports;
        }

        public Type Type { get; private set; }

        public string Id
        {
            get { return this.Type.Name; }
        }

        //public IEnumerable<ExportDefinition> ExportDefinitions
        //{
        //    get { return this.ExportDefinitionsOnType.Concat(this.ExportDefinitionsOnMembers.Values); }
        //}

        public IReadOnlyCollection<ExportDefinition> ExportDefinitionsOnType { get; private set; }

        public IReadOnlyDictionary<MemberInfo, ExportDefinition> ExportDefinitionsOnMembers { get; private set; }

        public IReadOnlyDictionary<MemberInfo, ImportDefinition> ImportDefinitions { get; private set; }
    }
}
