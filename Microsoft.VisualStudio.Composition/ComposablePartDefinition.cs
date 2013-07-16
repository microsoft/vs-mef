namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    [DebuggerDisplay("{Type.Name}")]
    public class ComposablePartDefinition
    {
        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exportsOnType, IReadOnlyDictionary<MemberInfo, ExportDefinition> exportsOnMembers, IReadOnlyDictionary<MemberInfo, ImportDefinition> imports, string sharingBoundary, MethodInfo onImportsSatisfied, IReadOnlyList<ImportDefinition> importingConstructor)
        {
            Requires.NotNull(partType, "partType");
            Requires.NotNull(exportsOnType, "exportsOnType");
            Requires.NotNull(exportsOnMembers, "exportsOnMembers");
            Requires.NotNull(imports, "imports");
            Requires.NotNull(importingConstructor, "importingConstructor");

            this.Type = partType;
            this.ExportDefinitionsOnType = exportsOnType;
            this.ExportDefinitionsOnMembers = exportsOnMembers;
            this.ImportDefinitions = imports;
            this.SharingBoundary = sharingBoundary;
            this.OnImportsSatisfied = onImportsSatisfied;
            this.ImportingConstructor = importingConstructor;

            this.CreationPolicy = this.IsShared ? MefV1.CreationPolicy.Shared : MefV1.CreationPolicy.NonShared;
        }

        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exportsOnType, IReadOnlyDictionary<MemberInfo, ExportDefinition> exportsOnMembers, IReadOnlyDictionary<MemberInfo, ImportDefinition> imports, string sharingBoundary, MethodInfo onImportsSatisfied, IReadOnlyList<ImportDefinition> importingConstructor, MefV1.CreationPolicy partCreationPolicy)
            : this(partType, exportsOnType, exportsOnMembers, imports, sharingBoundary, onImportsSatisfied, importingConstructor)
        {
            this.CreationPolicy = partCreationPolicy;
        }

        public Type Type { get; private set; }

        public string Id
        {
            get { return this.Type.Name; }
        }

        public string SharingBoundary { get; private set; }

        public MefV1.CreationPolicy CreationPolicy { get; private set; }

        public bool IsShared
        {
            get { return this.SharingBoundary != null; }
        }

        public MethodInfo OnImportsSatisfied { get; private set; }

        public IReadOnlyCollection<ExportDefinition> ExportDefinitionsOnType { get; private set; }

        public IReadOnlyDictionary<MemberInfo, ExportDefinition> ExportDefinitionsOnMembers { get; private set; }

        public IEnumerable<KeyValuePair<MemberInfo, ExportDefinition>> ExportDefinitions
        {
            get
            {
                foreach (var export in this.ExportDefinitionsOnType)
                {
                    yield return new KeyValuePair<MemberInfo, ExportDefinition>(null, export);
                }

                foreach (var export in this.ExportDefinitionsOnMembers)
                {
                    yield return export;
                }
            }
        }

        public IReadOnlyDictionary<MemberInfo, ImportDefinition> ImportDefinitions { get; private set; }

        /// <summary>
        /// Gets the list of parameters on the importing constructor.
        /// </summary>
        public IReadOnlyList<ImportDefinition> ImportingConstructor { get; private set; }
    }
}
