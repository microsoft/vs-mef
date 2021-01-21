// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ImportMetadataConstraintTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void ImportOneWithConstraint(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.IsType<ExportFirst>(part.FirstByOne);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyWithConstraintToOne(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.IsType<ExportSecond>(part.SecondByMany.Single());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyWithConstraintToTwo(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.Equal(2, part.OddNumberedExports.Count);
            Assert.Equal(1, part.OddNumberedExports.OfType<ExportFirst>().Count());
            Assert.Equal(1, part.OddNumberedExports.OfType<ExportThird>().Count());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyUnconstrained(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.Equal(3, part.UnconstrainedMany.Count);
        }

        [Export]
        public class ImportOneWithContraintPart
        {
            [Import("Common"), ImportMetadataConstraint("Name", "First")]
            public object FirstByOne { get; set; } = null!;

            [ImportMany("Common"), ImportMetadataConstraint("Name", "Second")]
            public ICollection<object> SecondByMany { get; set; } = null!;

            [ImportMany("Common"), ImportMetadataConstraint("Number", "Odd")]
            public ICollection<object> OddNumberedExports { get; set; } = null!;

            [ImportMany("Common")]
            public ICollection<object> UnconstrainedMany { get; set; } = null!;
        }

        [Export("Common", typeof(object))]
        [ExportMetadata("Name", "First")]
        [ExportMetadata("Number", "Odd")]
        public class ExportFirst { }

        [Export("Common", typeof(object))]
        [ExportMetadata("Name", "Second")]
        [ExportMetadata("Number", "Even")]
        public class ExportSecond { }

        [Export("Common", typeof(object))]
        [ExportMetadata("Name", "Third")]
        [ExportMetadata("Number", "Odd")]
        public class ExportThird { }
    }
}
