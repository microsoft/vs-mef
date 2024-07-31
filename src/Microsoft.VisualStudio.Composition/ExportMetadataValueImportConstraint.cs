// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class ExportMetadataValueImportConstraint : IImportSatisfiabilityConstraint, IDescriptiveToString
    {
        public ExportMetadataValueImportConstraint(string name, object? value)
        {
            Requires.NotNullOrEmpty(name, nameof(name));

            this.Name = name;
            this.Value = value;
        }

        public string Name { get; private set; }

        public object? Value { get; private set; }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, nameof(exportDefinition));

            object? exportMetadataValue;
            if (exportDefinition.Metadata.TryGetValue(this.Name, out exportMetadataValue))
            {
                if (EqualityComparer<object?>.Default.Equals(this.Value, exportMetadataValue))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Equals(IImportSatisfiabilityConstraint? obj)
        {
            var other = obj as ExportMetadataValueImportConstraint;
            if (other == null)
            {
                return false;
            }

            return this.Name == other.Name
                && EqualityComparer<object?>.Default.Equals(this.Value, other.Value);
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            indentingWriter.WriteLine("{0} = {1}", this.Name, this.Value);
        }
    }
}
