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
        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberInfo, ExportDefinition> exportingMembers, IReadOnlyDictionary<MemberInfo, ImportDefinition> importingMembers, string sharingBoundary, MethodInfo onImportsSatisfied, IReadOnlyList<ImportDefinition> importingConstructor)
        {
            Requires.NotNull(partType, "partType");
            Requires.NotNull(exportedTypes, "exportedTypes");
            Requires.NotNull(exportingMembers, "exportingMembers");
            Requires.NotNull(importingMembers, "importingMembers");
            Requires.NotNull(importingConstructor, "importingConstructor");

            this.Type = partType;
            this.ExportedTypes = exportedTypes;
            this.ExportingMembers = exportingMembers;
            this.ImportingMembers = importingMembers;
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

        /// <summary>
        /// Gets the types exported on the part itself.
        /// </summary>
        public IReadOnlyCollection<ExportDefinition> ExportedTypes { get; private set; }

        /// <summary>
        /// Gets the exports found on members of the part (exporting properties, fields, methods.)
        /// </summary>
        public IReadOnlyDictionary<MemberInfo, ExportDefinition> ExportingMembers { get; private set; }

        /// <summary>
        /// Gets a sequence of all exports found on this part (both the type directly and its members).
        /// </summary>
        public IEnumerable<KeyValuePair<MemberInfo, ExportDefinition>> ExportDefinitions
        {
            get
            {
                foreach (var export in this.ExportedTypes)
                {
                    yield return new KeyValuePair<MemberInfo, ExportDefinition>(null, export);
                }

                foreach (var export in this.ExportingMembers)
                {
                    yield return export;
                }
            }
        }

        public IReadOnlyDictionary<MemberInfo, ImportDefinition> ImportingMembers { get; private set; }

        /// <summary>
        /// Gets the list of parameters on the importing constructor.
        /// </summary>
        public IReadOnlyList<ImportDefinition> ImportingConstructor { get; private set; }

        public ConstructorInfo ImportingConstructorInfo {
            get { return this.Type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, this.ImportingConstructor.Select(i => i.MemberType).ToArray(), null); }
        }

        /// <summary>
        /// Gets a sequence of all imports found on this part (both members and importing constructor).
        /// </summary>
        public IEnumerable<ImportDefinition> ImportDefinitions
        {
            get { return this.ImportingMembers.Values.Concat(this.ImportingConstructor); }
        }
    }
}
