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

    [DebuggerDisplay("{" + nameof(ContractName) + ",nq}")]
    public class ExportDefinition : IEquatable<ExportDefinition>
    {
        public ExportDefinition(string contractName, IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNullOrEmpty(contractName, nameof(contractName));
            Requires.NotNull(metadata, nameof(metadata));

            this.ContractName = contractName;

            // Don't call ToImmutableDictionary() on the metadata. We have to trust that it's immutable
            // because forcing it to be immutable can defeat LazyMetadataWrapper's laziness, forcing
            // assembly loads and copying a dictionary when it's for practical interests immutable underneath anyway.
            this.Metadata = metadata;
        }

        public string ContractName { get; private set; }

        public IReadOnlyDictionary<string, object> Metadata { get; private set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ExportDefinition);
        }

        public override int GetHashCode()
        {
            return this.ContractName.GetHashCode();
        }

        public bool Equals(ExportDefinition other)
        {
            if (other == null)
            {
                return false;
            }

            bool result = this.ContractName == other.ContractName
                && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata);
            return result;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            indentingWriter.WriteLine("ContractName: {0}", this.ContractName);
            indentingWriter.WriteLine("Metadata:");
            using (indentingWriter.Indent())
            {
                foreach (var item in this.Metadata)
                {
                    indentingWriter.WriteLine("{0} = {1}", item.Key, item.Value);
                }
            }
        }

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            ReflectionHelpers.GetInputAssembliesFromMetadata(assemblies, this.Metadata);
        }
    }
}
