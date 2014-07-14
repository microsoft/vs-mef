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

    [DebuggerDisplay("{Type.Name}")]
    public class ComposablePartDefinition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComposablePartDefinition"/> class.
        /// </summary>
        /// <param name="partType">Type of the part.</param>
        /// <param name="exportedTypes">The exported types.</param>
        /// <param name="exportingMembers">The exporting members.</param>
        /// <param name="importingMembers">The importing members.</param>
        /// <param name="sharingBoundary">The sharing boundary that this part is shared within.</param>
        /// <param name="onImportsSatisfied">The method to invoke after satisfying imports, if any.</param>
        /// <param name="importingConstructor">The importing arguments taken by the importing constructor. <c>null</c> if the part cannot be instantiated.</param>
        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> exportingMembers, IReadOnlyList<ImportDefinitionBinding> importingMembers, string sharingBoundary, MethodInfo onImportsSatisfied, IReadOnlyList<ImportDefinitionBinding> importingConstructor, CreationPolicy partCreationPolicy)
        {
            Requires.NotNull(partType, "partType");
            Requires.NotNull(exportedTypes, "exportedTypes");
            Requires.NotNull(exportingMembers, "exportingMembers");
            Requires.NotNull(importingMembers, "importingMembers");

            this.Type = partType;
            this.ExportedTypes = exportedTypes;
            this.ExportingMembers = exportingMembers;
            this.ImportingMembers = importingMembers;
            this.SharingBoundary = sharingBoundary;
            this.OnImportsSatisfied = onImportsSatisfied;
            this.ImportingConstructor = importingConstructor;
            this.CreationPolicy = partCreationPolicy;
        }

        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exportsOnType, IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> exportsOnMembers, IReadOnlyList<ImportDefinitionBinding> imports, MethodInfo onImportsSatisfied, IReadOnlyList<ImportDefinitionBinding> importingConstructor, CreationPolicy partCreationPolicy)
            : this(partType, exportsOnType, exportsOnMembers, imports, partCreationPolicy != CreationPolicy.NonShared ? string.Empty : null, onImportsSatisfied, importingConstructor, partCreationPolicy)
        {
            this.IsSharingBoundaryInferred = partCreationPolicy != Composition.CreationPolicy.NonShared;
        }

        public Type Type { get; private set; }

        public string Id
        {
            get { return this.Type.FullName.Replace('`', '_').Replace('.', '_').Replace('+', '_'); }
        }

        public string SharingBoundary { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the sharing boundary must be inferred from what is imported.
        /// </summary>
        /// <remarks>
        /// This is <c>true</c> when the part was discovered by MEFv1 attributes, since these attributes do not have
        /// a way to convey a sharing boundary.
        /// This is <c>false</c> when the part is discovered by MEFv2 attributes, which have a SharedAttribute(string) that they can use
        /// to specify the value.
        /// When this is <c>true</c>, the <see cref="SharingBoundary"/> property is set to <see cref="String.Empty"/>.
        /// </remarks>
        public bool IsSharingBoundaryInferred { get; private set; }

        public CreationPolicy CreationPolicy { get; private set; }

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
        public IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> ExportingMembers { get; private set; }

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

                foreach (var member in this.ExportingMembers)
                {
                    foreach (var export in member.Value)
                    {
                        yield return new KeyValuePair<MemberInfo, ExportDefinition>(member.Key, export);
                    }
                }
            }
        }

        public IReadOnlyList<ImportDefinitionBinding> ImportingMembers { get; private set; }

        /// <summary>
        /// Gets the list of parameters on the importing constructor.
        /// </summary>
        public IReadOnlyList<ImportDefinitionBinding> ImportingConstructor { get; private set; }

        public bool IsInstantiable
        {
            get { return this.ImportingConstructor != null; }
        }

        public ConstructorInfo ImportingConstructorInfo
        {
            get
            {
                return this.ImportingConstructor != null
                    ? this.Type.GetTypeInfo().DeclaredConstructors.FirstOrDefault(ctor => !ctor.IsStatic && ctor.HasParameters(this.ImportingConstructor.Select(i => i.ImportingParameter.ParameterType).ToArray()))
                    : null;
            }
        }

        /// <summary>
        /// Gets a sequence of all imports found on this part (both members and importing constructor).
        /// </summary>
        public IEnumerable<ImportDefinitionBinding> Imports
        {
            get
            {
                IEnumerable<ImportDefinitionBinding> result = this.ImportingMembers;
                if (this.ImportingConstructor != null)
                {
                    result = result.Concat(this.ImportingConstructor);
                }

                return result;
            }
        }
    }
}
