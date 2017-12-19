// Copyright (c) Microsoft. All rights reserved.

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
    using Microsoft.VisualStudio.Composition.Reflection;

    [DebuggerDisplay("{" + nameof(Type) + ".Name}")]
    public class ComposablePartDefinition : IEquatable<ComposablePartDefinition>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComposablePartDefinition"/> class.
        /// </summary>
        /// <param name="partType">Type of the part.</param>
        /// <param name="metadata">The metadata discovered on the part.</param>
        /// <param name="exportedTypes">The exported types.</param>
        /// <param name="exportingMembers">The exporting members.</param>
        /// <param name="importingMembers">The importing members.</param>
        /// <param name="sharingBoundary">The sharing boundary that this part is shared within.</param>
        /// <param name="onImportsSatisfied">The method to invoke after satisfying imports, if any.</param>
        /// <param name="importingConstructorRef">The constructor to invoke to construct the part.</param>
        /// <param name="importingConstructorImports">The importing arguments taken by the importing constructor. <c>null</c> if the part cannot be instantiated.</param>
        /// <param name="partCreationPolicy">The creation policy for this part.</param>
        /// <param name="isSharingBoundaryInferred">A value indicating whether the part does not have an explicit sharing boundary, and therefore can obtain its sharing boundary based on its imports.</param>
        [Obsolete]
        public ComposablePartDefinition(TypeRef partType, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers, IEnumerable<ImportDefinitionBinding> importingMembers, string sharingBoundary, MethodRef onImportsSatisfied, ConstructorRef importingConstructorRef, IReadOnlyList<ImportDefinitionBinding> importingConstructorImports, CreationPolicy partCreationPolicy, bool isSharingBoundaryInferred = false)
            : this(partType, metadata, exportedTypes, exportingMembers, importingMembers, sharingBoundary, onImportsSatisfied, new MethodRef(importingConstructorRef), importingConstructorImports, partCreationPolicy, isSharingBoundaryInferred)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComposablePartDefinition"/> class.
        /// </summary>
        /// <param name="partType">Type of the part.</param>
        /// <param name="metadata">The metadata discovered on the part.</param>
        /// <param name="exportedTypes">The exported types.</param>
        /// <param name="exportingMembers">The exporting members.</param>
        /// <param name="importingMembers">The importing members.</param>
        /// <param name="sharingBoundary">The sharing boundary that this part is shared within.</param>
        /// <param name="onImportsSatisfied">The method to invoke after satisfying imports, if any.</param>
        /// <param name="importingConstructorRef">The constructor to invoke to construct the part.</param>
        /// <param name="importingConstructorImports">The importing arguments taken by the importing constructor. <c>null</c> if the part cannot be instantiated.</param>
        /// <param name="partCreationPolicy">The creation policy for this part.</param>
        /// <param name="isSharingBoundaryInferred">A value indicating whether the part does not have an explicit sharing boundary, and therefore can obtain its sharing boundary based on its imports.</param>
        public ComposablePartDefinition(TypeRef partType, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers, IEnumerable<ImportDefinitionBinding> importingMembers, string sharingBoundary, MethodRef onImportsSatisfied, MethodRef importingConstructorRef, IReadOnlyList<ImportDefinitionBinding> importingConstructorImports, CreationPolicy partCreationPolicy, bool isSharingBoundaryInferred = false)
        {
            Requires.NotNull(partType, nameof(partType));
            Requires.NotNull(metadata, nameof(metadata));
            Requires.NotNull(exportedTypes, nameof(exportedTypes));
            Requires.NotNull(exportingMembers, nameof(exportingMembers));
            Requires.NotNull(importingMembers, nameof(importingMembers));

            this.TypeRef = partType;
            this.Metadata = metadata;
            this.ExportedTypes = exportedTypes;
            this.ExportingMembers = exportingMembers;
            this.ImportingMembers = ImmutableHashSet.CreateRange(importingMembers);
            this.SharingBoundary = sharingBoundary;
            this.OnImportsSatisfiedRef = onImportsSatisfied;
            this.ImportingConstructorOrFactoryRef = importingConstructorRef;
            this.ImportingConstructorImports = importingConstructorImports;
            this.CreationPolicy = partCreationPolicy;
            this.IsSharingBoundaryInferred = isSharingBoundaryInferred;
            this.ExtraInputAssemblies = Enumerable.Empty<AssemblyName>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComposablePartDefinition"/> class.
        /// </summary>
        /// <param name="partType">Type of the part.</param>
        /// <param name="metadata">The metadata discovered on the part.</param>
        /// <param name="exportedTypes">The exported types.</param>
        /// <param name="exportingMembers">The exporting members.</param>
        /// <param name="importingMembers">The importing members.</param>
        /// <param name="sharingBoundary">The sharing boundary that this part is shared within.</param>
        /// <param name="onImportsSatisfied">The method to invoke after satisfying imports, if any.</param>
        /// <param name="importingConstructorRef">The constructor to invoke to construct the part.</param>
        /// <param name="importingConstructorImports">The importing arguments taken by the importing constructor. <c>null</c> if the part cannot be instantiated.</param>
        /// <param name="partCreationPolicy">The creation policy for this part.</param>
        /// <param name="extraInputAssemblies">A sequence of extra assemblies to be added to the set for <see cref="GetInputAssemblies(ISet{AssemblyName})"/></param>
        /// <param name="isSharingBoundaryInferred">A value indicating whether the part does not have an explicit sharing boundary, and therefore can obtain its sharing boundary based on its imports.</param>
        [Obsolete]
        public ComposablePartDefinition(TypeRef partType, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers, IEnumerable<ImportDefinitionBinding> importingMembers, string sharingBoundary, MethodRef onImportsSatisfied, ConstructorRef importingConstructorRef, IReadOnlyList<ImportDefinitionBinding> importingConstructorImports, CreationPolicy partCreationPolicy, IEnumerable<AssemblyName> extraInputAssemblies, bool isSharingBoundaryInferred = false)
            : this(
                  partType,
                  metadata,
                  exportedTypes,
                  exportingMembers,
                  importingMembers,
                  sharingBoundary,
                  onImportsSatisfied,
                  new MethodRef(importingConstructorRef),
                  importingConstructorImports,
                  partCreationPolicy,
                  extraInputAssemblies,
                  isSharingBoundaryInferred)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComposablePartDefinition"/> class.
        /// </summary>
        /// <param name="partType">Type of the part.</param>
        /// <param name="metadata">The metadata discovered on the part.</param>
        /// <param name="exportedTypes">The exported types.</param>
        /// <param name="exportingMembers">The exporting members.</param>
        /// <param name="importingMembers">The importing members.</param>
        /// <param name="sharingBoundary">The sharing boundary that this part is shared within.</param>
        /// <param name="onImportsSatisfied">The method to invoke after satisfying imports, if any.</param>
        /// <param name="importingConstructorRef">The constructor to invoke to construct the part.</param>
        /// <param name="importingConstructorImports">The importing arguments taken by the importing constructor. <c>null</c> if the part cannot be instantiated.</param>
        /// <param name="partCreationPolicy">The creation policy for this part.</param>
        /// <param name="extraInputAssemblies">A sequence of extra assemblies to be added to the set for <see cref="GetInputAssemblies(ISet{AssemblyName})"/></param>
        /// <param name="isSharingBoundaryInferred">A value indicating whether the part does not have an explicit sharing boundary, and therefore can obtain its sharing boundary based on its imports.</param>
        public ComposablePartDefinition(TypeRef partType, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers, IEnumerable<ImportDefinitionBinding> importingMembers, string sharingBoundary, MethodRef onImportsSatisfied, MethodRef importingConstructorRef, IReadOnlyList<ImportDefinitionBinding> importingConstructorImports, CreationPolicy partCreationPolicy, IEnumerable<AssemblyName> extraInputAssemblies, bool isSharingBoundaryInferred = false)
            : this(partType, metadata, exportedTypes, exportingMembers, importingMembers, sharingBoundary, onImportsSatisfied, importingConstructorRef, importingConstructorImports, partCreationPolicy, isSharingBoundaryInferred)
        {
            Requires.NotNull(extraInputAssemblies, nameof(extraInputAssemblies));
            this.ExtraInputAssemblies = extraInputAssemblies;
        }

        public Type Type
        {
            get { return this.TypeRef.Resolve(); }
        }

        public TypeRef TypeRef { get; private set; }

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
        /// When this is <c>true</c>, the <see cref="SharingBoundary"/> property is set to <see cref="string.Empty"/>.
        /// </remarks>
        public bool IsSharingBoundaryInferred { get; private set; }

        public CreationPolicy CreationPolicy { get; private set; }

        public bool IsShared
        {
            get { return this.SharingBoundary != null; }
        }

        /// <summary>
        /// Gets the metadata for this part.
        /// </summary>
        /// <remarks>
        /// This metadata has no effect on composition, but may be useful if the host
        /// wishes to filter a catalog based on part metadata prior to creating a composition.
        /// </remarks>
        public IReadOnlyDictionary<string, object> Metadata { get; private set; }

        public MethodInfo OnImportsSatisfied
        {
            get { return (MethodInfo)this.OnImportsSatisfiedRef.MethodBase; }
        }

        public MethodRef OnImportsSatisfiedRef { get; private set; }

        /// <summary>
        /// Gets the types exported on the part itself.
        /// </summary>
        public IReadOnlyCollection<ExportDefinition> ExportedTypes { get; private set; }

        /// <summary>
        /// Gets the exports found on members of the part (exporting properties, fields, methods.)
        /// </summary>
        public IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> ExportingMembers { get; private set; }

        /// <summary>
        /// Gets a sequence of all exports found on this part (both the type directly and its members).
        /// </summary>
        public IEnumerable<KeyValuePair<MemberRef, ExportDefinition>> ExportDefinitions
        {
            get
            {
                foreach (var export in this.ExportedTypes)
                {
                    yield return new KeyValuePair<MemberRef, ExportDefinition>(default(MemberRef), export);
                }

                foreach (var member in this.ExportingMembers)
                {
                    foreach (var export in member.Value)
                    {
                        yield return new KeyValuePair<MemberRef, ExportDefinition>(member.Key, export);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the sequence of extra input assemblies that will be added to the
        /// input assemblies for this <see cref="ComposablePartDefinition"/>.
        /// </summary>
        public IEnumerable<AssemblyName> ExtraInputAssemblies { get; }

        public ImmutableHashSet<ImportDefinitionBinding> ImportingMembers { get; private set; }

        /// <summary>
        /// Gets the list of parameters on the importing constructor,
        /// or <c>null</c> if the part cannot be instantiated.
        /// </summary>
        public IReadOnlyList<ImportDefinitionBinding> ImportingConstructorImports { get; private set; }

        public bool IsInstantiable
        {
            get { return this.ImportingConstructorImports != null; }
        }

        [Obsolete]
        public ConstructorRef ImportingConstructorRef => new ConstructorRef(this.ImportingConstructorOrFactoryRef.DeclaringType, this.ImportingConstructorOrFactoryRef.MetadataToken, this.ImportingConstructorOrFactoryRef.ParameterTypes);

        [Obsolete]
        public ConstructorInfo ImportingConstructorInfo
        {
            get { return this.ImportingConstructorRef.ConstructorInfo; }
        }

        public MethodRef ImportingConstructorOrFactoryRef { get; }

        public MethodBase ImportingConstructorOrFactory => this.ImportingConstructorOrFactoryRef.MethodBase;

        /// <summary>
        /// Gets a sequence of all imports found on this part (both members and importing constructor).
        /// </summary>
        public IEnumerable<ImportDefinitionBinding> Imports
        {
            get
            {
                IEnumerable<ImportDefinitionBinding> result = this.ImportingMembers;
                if (this.ImportingConstructorImports != null)
                {
                    result = result.Concat(this.ImportingConstructorImports);
                }

                return result;
            }
        }

        public override int GetHashCode()
        {
            return this.TypeRef.GetHashCode();
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

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            bool result = this.TypeRef.Equals(other.TypeRef)
                && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata)
                && this.SharingBoundary == other.SharingBoundary
                && this.IsSharingBoundaryInferred == other.IsSharingBoundaryInferred
                && this.CreationPolicy == other.CreationPolicy
                && this.OnImportsSatisfiedRef.Equals(other.OnImportsSatisfiedRef)
                && ByValueEquality.EquivalentIgnoreOrder<ExportDefinition>().Equals(this.ExportedTypes, other.ExportedTypes)
                && ByValueEquality.Dictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>(ByValueEquality.EquivalentIgnoreOrder<ExportDefinition>()).Equals(this.ExportingMembers, other.ExportingMembers)
                && this.ImportingConstructorOrFactoryRef.Equals(other.ImportingConstructorOrFactoryRef)
                && this.ImportingMembers.SetEquals(other.ImportingMembers)
                && ((this.ImportingConstructorImports == null && other.ImportingConstructorImports == null) || (this.ImportingConstructorImports != null && other.ImportingConstructorImports != null && this.ImportingConstructorImports.SequenceEqual(other.ImportingConstructorImports)));
            return result;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            indentingWriter.WriteLine("Type: {0}", this.Type.FullName);
            if (this.Metadata.Count > 0)
            {
                indentingWriter.WriteLine("Part metadata:");
                using (indentingWriter.Indent())
                {
                    foreach (var item in this.Metadata)
                    {
                        indentingWriter.WriteLine("{0} = {1}", item.Key, item.Value);
                    }
                }
            }

            indentingWriter.WriteLine("SharingBoundary: {0}", this.SharingBoundary.SpecifyIfNull());
            indentingWriter.WriteLine("IsSharingBoundaryInferred: {0}", this.IsSharingBoundaryInferred);
            indentingWriter.WriteLine("CreationPolicy: {0}", this.CreationPolicy);
            indentingWriter.WriteLine("OnImportsSatisfied: {0}", this.OnImportsSatisfied.SpecifyIfNull());

            indentingWriter.WriteLine("ExportedTypes:");
            using (indentingWriter.Indent())
            {
                foreach (var item in this.ExportedTypes.OrderBy(et => et.ContractName))
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
                    indentingWriter.WriteLine(exportingMember.Key.MemberInfo.Name);
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

            if (this.ImportingConstructorImports == null)
            {
                indentingWriter.WriteLine("ImportingConstructor: <null>");
            }
            else
            {
                indentingWriter.WriteLine("ImportingConstructor:");
                using (indentingWriter.Indent())
                {
                    foreach (var import in this.ImportingConstructorImports)
                    {
                        import.ToString(indentingWriter);
                    }
                }
            }
        }

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            foreach (var inputAssembly in this.ExtraInputAssemblies)
            {
                assemblies.Add(inputAssembly);
            }

            this.TypeRef.GetInputAssemblies(assemblies);
            ReflectionHelpers.GetInputAssembliesFromMetadata(assemblies, this.Metadata);
            foreach (var export in this.ExportedTypes)
            {
                export.GetInputAssemblies(assemblies);
            }

            foreach (var exportingMember in this.ExportingMembers)
            {
                exportingMember.Key.GetInputAssemblies(assemblies);
                foreach (var export in exportingMember.Value)
                {
                    export.GetInputAssemblies(assemblies);
                }
            }

            foreach (var import in this.Imports)
            {
                import.GetInputAssemblies(assemblies);
            }

            this.OnImportsSatisfiedRef.GetInputAssemblies(assemblies);
            this.ImportingConstructorOrFactoryRef.GetInputAssemblies(assemblies);
        }
    }
}
