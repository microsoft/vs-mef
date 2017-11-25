// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Composition;
    using Xunit;

    public class MetadataViewClassDefaultCtorProviderTests
    {
        private const string PublicPropertyExpectedValue = "Public Property Value";

        private const string InternalPropertyExpectedValue = "Internal Property Value";

        /* Tests to add:
         *   defaultValues?
         *   case sensitivity?
         *   exceptions thrown from setter?
         *   no default ctor, but has a constructor that takes some parameters.
         *   test properties without setters.
         */

        [MefFact(CompositionEngines.V2Compat, typeof(MetadataDecoratedPart), typeof(DefaultMetadataViewImporter))]
        public void PublicCtorPublicProperty(IContainer container)
        {
            var part = container.GetExportedValue<DefaultMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PublicProperty);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(MetadataDecoratedPart), typeof(DefaultMetadataViewImporter))]
        public void PublicCtorInternalProperty(IContainer container)
        {
            var part = container.GetExportedValue<DefaultMetadataViewImporter>();
            Assert.Null(part.InnerPart.Metadata.InternalProperty);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(MetadataDecoratedPart), typeof(InternalCtorMetadataViewImporter))]
        public void InternalCtorPublicProperty(IContainer container)
        {
            var part = container.GetExportedValue<InternalCtorMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PublicProperty);
        }

        [MefFact(CompositionEngines.V3EmulatingV2WithNonPublic, typeof(MetadataDecoratedPart), typeof(InternalMetadataViewImporter))]
        public void InternalTypePublicCtorPublicProperty(IContainer container)
        {
            var part = container.GetExportedValue<InternalMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PublicProperty);
        }

        public class DefaultMetadataView
        {
            public string PublicProperty { get; set; }

            internal string InternalProperty { get; set; }
        }

        [Export]
        public class DefaultMetadataViewImporter
        {
            [Import]
            public Lazy<MetadataDecoratedPart, DefaultMetadataView> InnerPart { get; set; }
        }

        [Export]
        public class InternalMetadataViewImporter
        {
            [Import]
            internal Lazy<MetadataDecoratedPart, DerivedView> InnerPart { get; set; }

            internal class DerivedView : DefaultMetadataView
            {
            }
        }

        [Export]
        public class InternalCtorMetadataViewImporter
        {
            [Import]
            public Lazy<MetadataDecoratedPart, DerivedView> InnerPart { get; set; }

            public class DerivedView : DefaultMetadataView
            {
                internal DerivedView() { }
            }
        }

        [Export]
        [ExportMetadata(nameof(DefaultMetadataView.PublicProperty), PublicPropertyExpectedValue)]
        [ExportMetadata(nameof(DefaultMetadataView.InternalProperty), InternalPropertyExpectedValue)]
        [ExportMetadata("Some extra property", "Some value")]
        [ExportMetadata("AnotherExtraProperty", "Some value")]
        public class MetadataDecoratedPart
        {
        }
    }
}
