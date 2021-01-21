// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class MetadataViewImplementationTests
    {
        [MefFact(CompositionEngines.V1Compat, typeof(ExportingPartWithoutMismatchingMetadata))]
        public void MetadataViewImplementationDirectQuery_WithoutMismatchingMetadata(IContainer container)
        {
            var export = container.GetExport<ExportingPartWithoutMismatchingMetadata, IMetadataView>();
            Assert.IsType<MetadataViewClass>(export.Metadata);
            Assert.Equal("1", export.Metadata.A);
            Assert.Null(export.Metadata.B);
            Assert.Equal(20, export.Metadata.PropertyWithMetadataThatDoesNotMatchType);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPart), typeof(ExportingPart))]
        public void MetadataViewImplementationDirectQuery(IContainer container)
        {
            var export = container.GetExport<ExportingPart, IMetadataView>();
            Assert.IsType<MetadataViewClass>(export.Metadata);
            Assert.Equal("1", export.Metadata.A);
            Assert.Null(export.Metadata.B);
            Assert.Equal(10, export.Metadata.PropertyWithMetadataThatDoesNotMatchType);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPartWithoutMismatchingMetadata), typeof(ExportingPartWithoutMismatchingMetadata))]
        public void MetadataViewImplementationViaImport_WithoutMismatchingMetadata(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithoutMismatchingMetadata>();
            Assert.IsType<MetadataViewClass>(importingPart.ImportingProperty!.Metadata);
            Assert.Equal("1", importingPart.ImportingProperty.Metadata.A);
            Assert.Null(importingPart.ImportingProperty.Metadata.B);
            Assert.Equal(20, importingPart.ImportingProperty.Metadata.PropertyWithMetadataThatDoesNotMatchType);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPart), typeof(ExportingPart))]
        public void MetadataViewImplementationViaImport(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            Assert.IsType<MetadataViewClass>(importingPart.ImportingProperty!.Metadata);
            Assert.Equal("1", importingPart.ImportingProperty.Metadata.A);
            Assert.Null(importingPart.ImportingProperty.Metadata.B);
            Assert.Equal(10, importingPart.ImportingProperty.Metadata.PropertyWithMetadataThatDoesNotMatchType);
        }

        [MefV1.Export]
        public class ImportingPartWithoutMismatchingMetadata
        {
            [MefV1.Import]
            public Lazy<ExportingPartWithoutMismatchingMetadata, IMetadataView>? ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class ImportingPart
        {
            [MefV1.Import]
            public Lazy<ExportingPart, IMetadataView>? ImportingProperty { get; set; }
        }

        [MefV1.Export]
        [MefV1.ExportMetadata("A", "1")]
        [MefV1.ExportMetadata("PropertyWithMetadataThatDoesNotMatchType", "valueThatDoesNotMatchType")]
        public class ExportingPart { }

        [MefV1.Export]
        [MefV1.ExportMetadata("A", "1")]
        public class ExportingPartWithoutMismatchingMetadata { }

        [MefV1.MetadataViewImplementation(typeof(MetadataViewClass))]
        public interface IMetadataView
        {
            string? A { get; }

            [DefaultValue("default")]
            string? B { get; }

            [DefaultValue("default")] // The value is ignored by MEFv1 when MetadataViewImplementation is present.
            int PropertyWithMetadataThatDoesNotMatchType { get; }
        }

        public class MetadataViewClass : IMetadataView
        {
            public MetadataViewClass(IDictionary<string, object> metadata)
            {
                this.A = (string?)(metadata.ContainsKey("A") ? metadata["A"] : null);
                this.B = (string?)(metadata.ContainsKey("B") ? metadata["B"] : null);
                this.PropertyWithMetadataThatDoesNotMatchType = metadata.ContainsKey("PropertyWithMetadataThatDoesNotMatchType") ? 10 : 20;
            }

            public string? A { get; set; }

            public string? B { get; set; }

            public int PropertyWithMetadataThatDoesNotMatchType { get; set; }
        }
    }
}
