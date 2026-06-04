// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
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

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ProjectedExportingPartA), typeof(ProjectedExportingPartAB), typeof(ProjectedExportingPartAWithWrongOptionalType))]
        public void MetadataViewImplementationWithMetadataViewBase_DirectQueryAppliesFilterAndDefaults(IContainer container)
        {
            var exports = container.GetExports<object, IProjectedMetadataView>("ProjectedExport").ToList();
            Assert.Equal(2, exports.Count);

            var partWithOptionalDefault = exports.Single(e => e.Value is ProjectedExportingPartA);
            Assert.IsType<ProjectedMetadataView>(partWithOptionalDefault.Metadata);
            Assert.Equal("1", partWithOptionalDefault.Metadata.A);
            Assert.Equal("default", partWithOptionalDefault.Metadata.B);
            Assert.Equal(new[] { "alpha", "beta" }, partWithOptionalDefault.Metadata.StringValues);
            Assert.Equal(new[] { 1, 2 }, partWithOptionalDefault.Metadata.IntValues);

            var partWithOptionalValue = exports.Single(e => e.Value is ProjectedExportingPartAB);
            Assert.Equal("1", partWithOptionalValue.Metadata.A);
            Assert.Equal("2", partWithOptionalValue.Metadata.B);
            Assert.Equal(new[] { "gamma", "delta" }, partWithOptionalValue.Metadata.StringValues);
            Assert.Equal(new[] { 3, 4 }, partWithOptionalValue.Metadata.IntValues);
        }

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ProjectedExportingPartA))]
        public void MetadataViewDerivedClassCannotBeUsedDirectlyAsMetadataType(IContainer container)
        {
            var ex = Assert.Throws<NotSupportedException>(() => container.GetExports<object, ProjectedMetadataView>("ProjectedExport").ToList());
            Assert.Contains(typeof(ProjectedMetadataView).FullName!, ex.Message);
        }

        [Fact]
        public async Task MetadataViewDerivedClassImportIsRejectedDuringConfiguration()
        {
            var configuration = await TestUtilities.CreateConfigurationAsync(
                CompositionEngines.V1,
                typeof(ProjectedExportingPartA),
                typeof(DirectProjectedMetadataViewImporter));

            Assert.False(configuration.CompositionErrors.IsEmpty);
            var rootCauses = configuration.CompositionErrors.Peek();
            Assert.All(rootCauses, error => Assert.Equal(typeof(DirectProjectedMetadataViewImporter), Assert.Single(error.Parts).Definition.Type));
            Assert.Contains(rootCauses, error => error.Message.Contains(typeof(ProjectedMetadataView).FullName!, StringComparison.Ordinal));
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3AllowConfigurationWithErrors, typeof(ProjectedExportingPartA), typeof(DirectProjectedMetadataViewImporter), typeof(UnrelatedProjectedExport), InvalidConfiguration = true)]
        public void MetadataViewDerivedClassImportRejectsPartButRetainsUnrelatedParts(IContainer container)
        {
            Assert.Empty(container.GetExportedValues<DirectProjectedMetadataViewImporter>());
            Assert.NotNull(container.GetExportedValue<UnrelatedProjectedExport>());
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

        [MefV1.MetadataViewImplementation(typeof(ProjectedMetadataView))]
        public interface IProjectedMetadataView
        {
            string? A { get; }

            [DefaultValue("default")]
            string? B { get; }

            string[] StringValues { get; }

            int[] IntValues { get; }
        }

        public class ProjectedMetadataView : MetadataView, IProjectedMetadataView
        {
            public string? A => this.GetMetadata<string?>();

            public string? B => this.GetMetadata<string?>();

            public string[] StringValues => this.GetMetadata<string[]>();

            public int[] IntValues => this.GetMetadata<int[]>();
        }

        [MefV1.Export("ProjectedExport", typeof(object))]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.A), "1")]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.StringValues), new[] { "alpha", "beta" })]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.IntValues), new[] { 1, 2 })]
        public class ProjectedExportingPartA
        {
        }

        [MefV1.Export("ProjectedExport", typeof(object))]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.A), "1")]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.B), "2")]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.StringValues), new[] { "gamma", "delta" })]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.IntValues), new[] { 3, 4 })]
        public class ProjectedExportingPartAB
        {
        }

        [MefV1.Export("ProjectedExport", typeof(object))]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.A), "1")]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.B), 2)]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.StringValues), new[] { "filtered" })]
        [MefV1.ExportMetadata(nameof(IProjectedMetadataView.IntValues), new[] { 5 })]
        public class ProjectedExportingPartAWithWrongOptionalType
        {
        }

        [MefV1.Export]
        public class DirectProjectedMetadataViewImporter
        {
            [MefV1.Import]
            public Lazy<object, ProjectedMetadataView>? ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class UnrelatedProjectedExport
        {
        }
    }
}
