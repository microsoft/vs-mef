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
    using MessagePack;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    [DebuggerDisplay("{" + nameof(ContractName) + ",nq} ({Cardinality})")]
    [MessagePackObject(keyAsPropertyName: true)] // does not work with index and require cusotme formatter 
    public class ImportDefinition : IEquatable<ImportDefinition>
    {
       // [Key(5)]
        private readonly ImmutableList<IImportSatisfiabilityConstraint> exportConstraints;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinition"/> class
        /// based on MEF v2 attributes.
        /// </summary>
        public ImportDefinition(string contractName, ImportCardinality cardinality, IReadOnlyDictionary<string, object?> metadata, IReadOnlyCollection<IImportSatisfiabilityConstraint> exportConstraints, IReadOnlyCollection<string> exportFactorySharingBoundaries)
        {
            Requires.NotNullOrEmpty(contractName, nameof(contractName));
            Requires.NotNull(metadata, nameof(metadata));
            Requires.NotNull(exportConstraints, nameof(exportConstraints));
            Requires.NotNull(exportFactorySharingBoundaries, nameof(exportFactorySharingBoundaries));

            this.ContractName = contractName;
            this.Cardinality = cardinality;
            this.Metadata = metadata; // don't clone metadata as that will defeat lazy assembly loads when metadata values would require it.
            this.exportConstraints = exportConstraints.ToImmutableList();
            this.ExportFactorySharingBoundaries = exportFactorySharingBoundaries.ToImmutableHashSet();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinition"/> class
        /// based on MEF v1 attributes.
        /// </summary>
        public ImportDefinition(string contractName, ImportCardinality cardinality, IReadOnlyDictionary<string, object?> metadata, IReadOnlyCollection<IImportSatisfiabilityConstraint> exportConstraints)
            : this(contractName, cardinality, metadata, exportConstraints, ImmutableHashSet.Create<string>())
        {
        }

       // [Key(0)]
        public string ContractName { get; private set; }

      //  [Key(1)]
        public ImportCardinality Cardinality { get; private set; }

        /// <summary>
        /// Gets the sharing boundaries created when the export factory is used.
        /// </summary>
       // [Key(2)]
        public IReadOnlyCollection<string> ExportFactorySharingBoundaries { get; private set; }

      //  [Key(3)]
        public IReadOnlyDictionary<string, object?> Metadata { get; private set; }

      //  [Key(4)]
        public IReadOnlyCollection<IImportSatisfiabilityConstraint> ExportConstraints
        {
            get { return this.exportConstraints; }
        }

        public ImportDefinition WithExportConstraints(IReadOnlyCollection<IImportSatisfiabilityConstraint> constraints)
        {
            return new ImportDefinition(
                this.ContractName,
                this.Cardinality,
                this.Metadata,
                constraints,
                this.ExportFactorySharingBoundaries);
        }

        public ImportDefinition AddExportConstraint(IImportSatisfiabilityConstraint constraint)
        {
            Requires.NotNull(constraint, nameof(constraint));
            return this.WithExportConstraints(this.exportConstraints.Add(constraint));
        }

        public override int GetHashCode()
        {
            return this.ContractName.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return this.Equals(obj as ImportDefinition);
        }

        public bool Equals(ImportDefinition? other)
        {
            if (other == null)
            {
                return false;
            }

            bool result = this.ContractName == other.ContractName
                && this.Cardinality == other.Cardinality
                && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata)
                && ByValueEquality.EquivalentIgnoreOrder<IImportSatisfiabilityConstraint>().Equals(this.ExportConstraints, other.ExportConstraints)
                && ByValueEquality.EquivalentIgnoreOrder<string>().Equals(this.ExportFactorySharingBoundaries, other.ExportFactorySharingBoundaries);
            return result;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);

            indentingWriter.WriteLine("ContractName: {0}", this.ContractName);
            indentingWriter.WriteLine("Cardinality: {0}", this.Cardinality);
            indentingWriter.WriteLine("Metadata:");
            using (indentingWriter.Indent())
            {
                this.Metadata.ToString(indentingWriter);
            }

            indentingWriter.WriteLine("ExportFactorySharingBoundaries: {0}", string.Join(", ", this.ExportFactorySharingBoundaries));

            indentingWriter.WriteLine("ExportConstraints: ");
            using (indentingWriter.Indent())
            {
                foreach (var item in this.ExportConstraints.OrderBy(ec => ec.GetType().Name))
                {
                    indentingWriter.WriteLine(item.GetType().Name);
                    using (indentingWriter.Indent())
                    {
                        item.ToString(indentingWriter);
                    }
                }
            }
        }

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies, Func<Assembly, AssemblyName> nameRetriever)
        {
            Requires.NotNull(assemblies, nameof(assemblies));
            Requires.NotNull(nameRetriever, nameof(nameRetriever));

            // TODO: consider the assembly dependencies brought in by constraints.
            ReflectionHelpers.GetInputAssembliesFromMetadata(assemblies, this.Metadata, nameRetriever);
        }
    }
}
