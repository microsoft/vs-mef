namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    [DebuggerDisplay("{Type.Name}")]
    public class ComposablePartDefinition : IEquatable<ComposablePartDefinition>
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
        /// <param name="partCreationPolicy">The creation policy for this part.</param>
        /// <param name="isSharingBoundaryInferred">A value indicating whether the part does not have an explicit sharing boundary, and therefore can obtain its sharing boundary based on its imports.</param>
        public ComposablePartDefinition(Type partType, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberInfo, IReadOnlyCollection<ExportDefinition>> exportingMembers, IReadOnlyList<ImportDefinitionBinding> importingMembers, string sharingBoundary, MethodInfo onImportsSatisfied, IReadOnlyList<ImportDefinitionBinding> importingConstructor, CreationPolicy partCreationPolicy, bool isSharingBoundaryInferred = false)
        {
            Requires.NotNull(partType, "partType");
            Requires.NotNull(exportedTypes, "exportedTypes");
            Requires.NotNull(exportingMembers, "exportingMembers");
            Requires.NotNull(importingMembers, "importingMembers");

            this.Type = partType;
            this.ExportedTypes = exportedTypes;
            this.ExportingMembers = exportingMembers;
            this.ImportingMembers = ImmutableHashSet.CreateRange(importingMembers);
            this.SharingBoundary = sharingBoundary;
            this.OnImportsSatisfied = onImportsSatisfied;
            this.ImportingConstructor = importingConstructor;
            this.CreationPolicy = partCreationPolicy;
            this.IsSharingBoundaryInferred = isSharingBoundaryInferred;
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
        public IReadOnlyDictionary<MemberInfo, IReadOnlyCollection<ExportDefinition>> ExportingMembers { get; private set; }

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

        public ImmutableHashSet<ImportDefinitionBinding> ImportingMembers { get; private set; }

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

        public override int GetHashCode()
        {
            return this.Type.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ComposablePartDefinition);
        }

        public bool Equals(ComposablePartDefinition other)
        {
            if (other == null)
            {
                return false;
            }

            bool result = this.Type == other.Type
                && this.SharingBoundary == other.SharingBoundary
                && this.IsSharingBoundaryInferred == other.IsSharingBoundaryInferred
                && this.CreationPolicy == other.CreationPolicy
                && this.OnImportsSatisfied == other.OnImportsSatisfied
                && ByValueEquality.EquivalentIgnoreOrder<ExportDefinition>().Equals(this.ExportedTypes, other.ExportedTypes)
                && ByValueEquality.Dictionary<MemberInfo, IReadOnlyCollection<ExportDefinition>>(ByValueEquality.EquivalentIgnoreOrder<ExportDefinition>()).Equals(this.ExportingMembers, other.ExportingMembers)
                && this.ImportingMembers.SetEquals(other.ImportingMembers)
                && ((this.ImportingConstructor == null && other.ImportingConstructor == null) || (this.ImportingConstructor != null && other.ImportingConstructor != null && this.ImportingConstructor.SequenceEqual(other.ImportingConstructor)));
            return result;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            indentingWriter.WriteLine("Type: {0}", this.Type.FullName);
            indentingWriter.WriteLine("SharingBoundary: {0}", this.SharingBoundary.SpecifyIfNull());
            indentingWriter.WriteLine("CreationPolicy: {0}", this.CreationPolicy);
            indentingWriter.WriteLine("OnImportsSatisfied: {0}", this.OnImportsSatisfied.SpecifyIfNull());

            indentingWriter.WriteLine("ExportedTypes:");
            using (indentingWriter.Indent())
            {
                foreach (var item in this.ExportedTypes)
                {
                    indentingWriter.WriteLine("ExportDefinition");
                    using (indentingWriter.Indent())
                    {
                        item.ToString(indentingWriter);
                    }
                }
            }

            indentingWriter.WriteLine("ExportingMembers:");
            using (indentingWriter.Indent())
            {
                foreach (var exportingMember in this.ExportingMembers)
                {
                    indentingWriter.WriteLine(exportingMember.Key.Name);
                    using (indentingWriter.Indent())
                    {
                        foreach (var export in exportingMember.Value)
                        {
                            export.ToString(indentingWriter);
                        }
                    }
                }
            }

            indentingWriter.WriteLine("ImportingMembers:");
            using (indentingWriter.Indent())
            {
                foreach (var importingMember in this.ImportingMembers)
                {
                    importingMember.ToString(indentingWriter);
                }
            }

            if (this.ImportingConstructor == null)
            {
                indentingWriter.WriteLine("ImportingConstructor: <null>");
            }
            else
            {
                indentingWriter.WriteLine("ImportingConstructor:");
                using (indentingWriter.Indent())
                {
                    foreach (var import in this.ImportingConstructor)
                    {
                        import.ToString(indentingWriter);
                    }
                }
            }
        }
    }
}
