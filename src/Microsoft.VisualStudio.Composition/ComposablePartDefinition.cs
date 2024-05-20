// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using MessagePack;
    using Microsoft.VisualStudio.Composition.Formatter;
    using Microsoft.VisualStudio.Composition.Reflection;

    [DebuggerDisplay("{" + nameof(Type) + ".Name}")]
    [MessagePackFormatter(typeof(ComposablePartDefinitionFormatter))]
    public class ComposablePartDefinition : IEquatable<ComposablePartDefinition>
    {
        /// <inheritdoc cref="ComposablePartDefinition(TypeRef, IReadOnlyDictionary{string, object?}, IReadOnlyCollection{ExportDefinition}, IReadOnlyDictionary{MemberRef, IReadOnlyCollection{ExportDefinition}}, IEnumerable{ImportDefinitionBinding}, string?, IReadOnlyList{MethodRef}, MethodRef?, IReadOnlyList{ImportDefinitionBinding}?, CreationPolicy, bool, IEnumerable{AssemblyName})" />
        public ComposablePartDefinition(TypeRef partType, IReadOnlyDictionary<string, object?> metadata, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers, IEnumerable<ImportDefinitionBinding> importingMembers, string? sharingBoundary, IReadOnlyList<MethodRef> onImportsSatisfiedMethods, MethodRef? importingConstructorRef, IReadOnlyList<ImportDefinitionBinding>? importingConstructorImports, CreationPolicy partCreationPolicy)
            : this(partType, metadata, exportedTypes, exportingMembers, importingMembers, sharingBoundary, onImportsSatisfiedMethods, importingConstructorRef, importingConstructorImports, partCreationPolicy, isSharingBoundaryInferred: false)
        {
        }

        /// <inheritdoc cref="ComposablePartDefinition(TypeRef, IReadOnlyDictionary{string, object?}, IReadOnlyCollection{ExportDefinition}, IReadOnlyDictionary{MemberRef, IReadOnlyCollection{ExportDefinition}}, IEnumerable{ImportDefinitionBinding}, string?, IReadOnlyList{MethodRef}, MethodRef?, IReadOnlyList{ImportDefinitionBinding}?, CreationPolicy, bool, IEnumerable{AssemblyName})" />
        public ComposablePartDefinition(TypeRef partType, IReadOnlyDictionary<string, object?> metadata, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers, IEnumerable<ImportDefinitionBinding> importingMembers, string? sharingBoundary, IReadOnlyList<MethodRef> onImportsSatisfiedMethods, MethodRef? importingConstructorRef, IReadOnlyList<ImportDefinitionBinding>? importingConstructorImports, CreationPolicy partCreationPolicy, bool isSharingBoundaryInferred)
        {
            Requires.NotNull(partType, nameof(partType));
            Requires.NotNull(metadata, nameof(metadata));
            Requires.NotNull(exportedTypes, nameof(exportedTypes));
            Requires.NotNull(exportingMembers, nameof(exportingMembers));
            Requires.NotNull(importingMembers, nameof(importingMembers));
            Requires.NotNull(onImportsSatisfiedMethods, nameof(onImportsSatisfiedMethods));

            this.TypeRef = partType;
            this.Metadata = metadata;
            this.ExportedTypes = exportedTypes;
            this.ExportingMembers = exportingMembers;
            this.ImportingMembers = ImmutableHashSet.CreateRange(importingMembers);
            this.SharingBoundary = sharingBoundary;
            this.OnImportsSatisfiedMethodRefs = onImportsSatisfiedMethods;
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
        /// <param name="onImportsSatisfiedMethods">The method to invoke after satisfying imports, if any.</param>
        /// <param name="importingConstructorRef">The constructor to invoke to construct the part.</param>
        /// <param name="importingConstructorImports">The importing arguments taken by the importing constructor. <see langword="null"/> if the part cannot be instantiated.</param>
        /// <param name="partCreationPolicy">The creation policy for this part.</param>
        /// <param name="isSharingBoundaryInferred">A value indicating whether the part does not have an explicit sharing boundary, and therefore can obtain its sharing boundary based on its imports.</param>
        /// <param name="extraInputAssemblies">A sequence of extra assemblies to be added to the set for <see cref="GetInputAssemblies(ISet{AssemblyName}, Func{Assembly, AssemblyName})"/>.</param>
        public ComposablePartDefinition(TypeRef partType, IReadOnlyDictionary<string, object?> metadata, IReadOnlyCollection<ExportDefinition> exportedTypes, IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers, IEnumerable<ImportDefinitionBinding> importingMembers, string? sharingBoundary, IReadOnlyList<MethodRef> onImportsSatisfiedMethods, MethodRef? importingConstructorRef, IReadOnlyList<ImportDefinitionBinding>? importingConstructorImports, CreationPolicy partCreationPolicy, bool isSharingBoundaryInferred, IEnumerable<AssemblyName> extraInputAssemblies)
            : this(partType, metadata, exportedTypes, exportingMembers, importingMembers, sharingBoundary, onImportsSatisfiedMethods, importingConstructorRef, importingConstructorImports, partCreationPolicy, isSharingBoundaryInferred)
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
            get { return this.Type.FullName!.Replace('`', '_').Replace('.', '_').Replace('+', '_'); }
        }

        public string? SharingBoundary { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the sharing boundary must be inferred from what is imported.
        /// </summary>
        /// <remarks>
        /// This is <see langword="true"/> when the part was discovered by MEFv1 attributes, since these attributes do not have
        /// a way to convey a sharing boundary.
        /// This is <see langword="false"/> when the part is discovered by MEFv2 attributes, which have a SharedAttribute(string) that they can use
        /// to specify the value.
        /// When this is <see langword="true"/>, the <see cref="SharingBoundary"/> property is set to <see cref="string.Empty"/>.
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
        public IReadOnlyDictionary<string, object?> Metadata { get; private set; }

        /// <inheritdoc cref="OnImportsSatisfiedMethodRefs" />
        public IEnumerable<MethodInfo> OnImportsSatisfiedMethods => this.OnImportsSatisfiedMethodRefs.Select(m => (MethodInfo)m.MethodBase);

        /// <summary>
        /// Gets the list of methods to invoke after imports are satisfied.
        /// </summary>
        public IReadOnlyList<MethodRef> OnImportsSatisfiedMethodRefs { get; private set; }

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
        public IEnumerable<KeyValuePair<MemberRef?, ExportDefinition>> ExportDefinitions
        {
            get
            {
                foreach (var export in this.ExportedTypes)
                {
                    yield return new KeyValuePair<MemberRef?, ExportDefinition>(default(MemberRef), export);
                }

                foreach (var member in this.ExportingMembers)
                {
                    foreach (var export in member.Value)
                    {
                        yield return new KeyValuePair<MemberRef?, ExportDefinition>(member.Key, export);
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
        /// or <see langword="null"/> if the part cannot be instantiated.
        /// </summary>
        public IReadOnlyList<ImportDefinitionBinding>? ImportingConstructorImports { get; private set; }

        public bool IsInstantiable
        {
            get { return this.ImportingConstructorImports != null; }
        }

        public MethodRef? ImportingConstructorOrFactoryRef { get; }

        public MethodBase? ImportingConstructorOrFactory => this.ImportingConstructorOrFactoryRef?.MethodBase;

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

        public override bool Equals(object? obj)
        {
            return this.Equals(obj as ComposablePartDefinition);
        }

        public bool Equals(ComposablePartDefinition? other)
        {
            if (other == null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            bool result = EqualityComparer<TypeRef>.Default.Equals(this.TypeRef, other.TypeRef)
                && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata)
                && this.SharingBoundary == other.SharingBoundary
                && this.IsSharingBoundaryInferred == other.IsSharingBoundaryInferred
                && this.CreationPolicy == other.CreationPolicy
                && this.OnImportsSatisfiedMethodRefs.SequenceEqual(other.OnImportsSatisfiedMethodRefs, EqualityComparer<MethodRef?>.Default)
                && ByValueEquality.EquivalentIgnoreOrder<ExportDefinition>().Equals(this.ExportedTypes, other.ExportedTypes)
                && ByValueEquality.Dictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>(ByValueEquality.EquivalentIgnoreOrder<ExportDefinition>()).Equals(this.ExportingMembers, other.ExportingMembers)
                && EqualityComparer<MethodRef?>.Default.Equals(this.ImportingConstructorOrFactoryRef, other.ImportingConstructorOrFactoryRef)
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
            indentingWriter.WriteLine("OnImportsSatisfied:");
            using (indentingWriter.Indent())
            {
                foreach (MethodRef method in this.OnImportsSatisfiedMethodRefs)
                {
                    indentingWriter.WriteLine(method);
                }
            }

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

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies, Func<Assembly, AssemblyName> nameRetriever)
        {
            Requires.NotNull(assemblies, nameof(assemblies));
            Requires.NotNull(nameRetriever, nameof(nameRetriever));

            foreach (var inputAssembly in this.ExtraInputAssemblies)
            {
                assemblies.Add(inputAssembly);
            }

            this.TypeRef.GetInputAssemblies(assemblies);
            ReflectionHelpers.GetInputAssembliesFromMetadata(assemblies, this.Metadata, nameRetriever);
            foreach (var export in this.ExportedTypes)
            {
                export.GetInputAssemblies(assemblies, nameRetriever);
            }

            foreach (var exportingMember in this.ExportingMembers)
            {
                exportingMember.Key.GetInputAssemblies(assemblies);
                foreach (var export in exportingMember.Value)
                {
                    export.GetInputAssemblies(assemblies, nameRetriever);
                }
            }

            foreach (var import in this.Imports)
            {
                import.GetInputAssemblies(assemblies, nameRetriever);
            }

            foreach (var method in this.OnImportsSatisfiedMethodRefs)
            {
                method.GetInputAssemblies(assemblies);
            }

            this.ImportingConstructorOrFactoryRef?.GetInputAssemblies(assemblies);
        }
    }
}
