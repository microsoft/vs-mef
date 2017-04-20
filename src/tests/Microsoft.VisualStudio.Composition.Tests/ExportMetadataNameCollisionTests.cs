// Copyright (c) Microsoft. All rights reserved.

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

    [Trait("Ambiguous", "MetadataName")]
    public class ExportMetadataNameCollisionTests
    {
        [MefFact(CompositionEngines.V1Compat)]
        public void ExportMetadataNameCollision(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            IMetadata1 metadata1 = importingPart.ImportingProperty.Metadata;
            IMetadata2 metadata2 = importingPart.ImportingProperty.Metadata;
            Assert.Equal("SomeValue", metadata1.SomeName);
            Assert.Equal("SomeValue", metadata2.SomeName);
        }

        [Export]
        [MefV1.Export]
        [ExportMetadata("SomeName", "SomeValue")]
        [MefV1.ExportMetadata("SomeName", "SomeValue")]
        public class ExportWithMetadata { }

        [Export]
        [MefV1.Export]
        public class ImportingPart
        {
            [Import]
            [MefV1.Import]
            public Lazy<ExportWithMetadata, IMetadata> ImportingProperty { get; set; }
        }

        public interface IMetadata1
        {
            string SomeName { get; }
        }

        public interface IMetadata2
        {
            string SomeName { get; }
        }

        public interface IMetadata : IMetadata1, IMetadata2 { }
    }
}
