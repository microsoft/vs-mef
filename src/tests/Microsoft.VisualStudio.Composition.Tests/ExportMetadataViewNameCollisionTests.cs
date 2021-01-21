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
    using MefV1 = System.ComponentModel.Composition;

    public class ExportMetadataViewNameCollisionTests
    {
        [MefFact(CompositionEngines.V1Compat)]
        public void MetadataViewNameCollision(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportingPart1>();
            Assert.NotNull(part1.ImportingProperty.Metadata);

            var part2 = container.GetExportedValue<ImportingPart2>();
            Assert.NotNull(part2.ImportingProperty.Metadata);
        }

        [MefV1.Export]
        public class ImportingPart1
        {
            [MefV1.Import]
            public Lazy<ExportingPart, SubNS1.IMetadata> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        public class ImportingPart2
        {
            [MefV1.Import]
            public Lazy<ExportingPart, SubNS2.IMetadata> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        public class ExportingPart { }
    }

#pragma warning disable SA1403 // File may only contain a single namespace
    namespace SubNS1
    {
        public interface IMetadata { }
    }

    namespace SubNS2
    {
        public interface IMetadata { }
    }
#pragma warning restore SA1403 // File may only contain a single namespace
}
