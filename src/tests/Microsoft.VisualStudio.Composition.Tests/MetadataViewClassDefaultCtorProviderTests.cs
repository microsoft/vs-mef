// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.ComponentModel;
    using System.Composition;
    using Xunit;

    public class MetadataViewClassDefaultCtorProviderTests
    {
        private const string PublicPropertyExpectedValue = "Public Property Value";

        private const string InternalPropertyExpectedValue = "Internal Property Value";

        private const string DefaultPropertyExpectedValue = "Some default value";

        /* Tests to add:
         *   private properties (or private setters) on base class
         *   case sensitivity?
         *   exceptions thrown from setter?
         *   no default ctor, but has a constructor that takes some parameters.
         */

        [MefFact(CompositionEngines.V2Compat, typeof(MetadataDecoratedPart), typeof(DefaultMetadataViewImporter))]
        public void PublicCtorPublicProperty(IContainer container)
        {
            var part = container.GetExportedValue<DefaultMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PublicProperty);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(MetadataDecoratedPart), typeof(DefaultMetadataViewImporter))]
        public void DefaultProperty(IContainer container)
        {
            var part = container.GetExportedValue<DefaultMetadataViewImporter>();
            Assert.Equal(DefaultPropertyExpectedValue, part.InnerPart.Metadata.PropertyWithDefault);
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

            public string UnsettableProperty => null;

            [DefaultValue(DefaultPropertyExpectedValue)]
            public string PropertyWithDefault { get; set; }

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
        [ExportMetadata(nameof(DefaultMetadataView.UnsettableProperty), "Any value")]
        [ExportMetadata("Some extra property", "Some value")]
        [ExportMetadata("AnotherExtraProperty", "Some value")]
        public class MetadataDecoratedPart
        {
        }
    }
}
