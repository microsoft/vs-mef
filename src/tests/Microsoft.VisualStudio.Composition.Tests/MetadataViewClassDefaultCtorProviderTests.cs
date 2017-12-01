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

        [MefFact(CompositionEngines.V2Compat, typeof(MetadataDecoratedPart), typeof(DefaultMetadataViewImporter))]
        public void PublicCtorPublicProperty(IContainer container)
        {
            var part = container.GetExportedValue<DefaultMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PublicProperty);
        }

        [MefFact(CompositionEngines.V2, typeof(MetadataDecoratedPart), typeof(DerivedMetadataViewImporter), NoCompatGoal = true)]
        public void PropertyWithPrivateSetterInBaseClass_V2(IContainer container)
        {
            var part = container.GetExportedValue<DerivedMetadataViewImporter>();
            Assert.Null(part.InnerPart.Metadata.PropertyWithPrivateSetter);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(MetadataDecoratedPart), typeof(DerivedMetadataViewImporter))]
        public void PropertyWithPrivateSetterInBaseClass_V3(IContainer container)
        {
            var part = container.GetExportedValue<DerivedMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PropertyWithPrivateSetter);
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

        [MefFact(CompositionEngines.V2Compat, typeof(MetadataDecoratedPart), typeof(InternalCtorMetadataViewImporter), InvalidConfiguration = true)]
        public void InternalCtorPublicProperty(IContainer container)
        {
            var part = container.GetExportedValue<InternalCtorMetadataViewImporter>();
        }

        [MefFact(CompositionEngines.V2Compat, typeof(MetadataDecoratedPart), typeof(NoDefaultCtorMetadataViewImporter), InvalidConfiguration = true)]
        public void NoDefaultCtor(IContainer container)
        {
            var part = container.GetExportedValue<NoDefaultCtorMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PublicProperty);
        }

        [MefFact(CompositionEngines.V3EmulatingV2WithNonPublic, typeof(MetadataDecoratedPart), typeof(InternalMetadataViewImporter))]
        public void InternalTypePublicCtorPublicProperty(IContainer container)
        {
            var part = container.GetExportedValue<InternalMetadataViewImporter>();
            Assert.Equal(PublicPropertyExpectedValue, part.InnerPart.Metadata.PublicProperty);
        }

        [MefFact(CompositionEngines.V2, typeof(MetadataDecoratedPart), typeof(PropertySetterThrowsMetadataViewImporter))]
        public void MetadataViewPropertySetterThrows_V2(IContainer container)
        {
            Assert.Throws<InvalidOperationException>(() => container.GetExportedValue<PropertySetterThrowsMetadataViewImporter>());
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(MetadataDecoratedPart), typeof(PropertySetterThrowsMetadataViewImporter))]
        public void MetadataViewPropertySetterThrows_V3(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<PropertySetterThrowsMetadataViewImporter>());
        }

        public class DefaultMetadataView
        {
            public string PublicProperty { get; set; }

            public string UnsettableProperty => null;

            [DefaultValue(DefaultPropertyExpectedValue)]
            public string PropertyWithDefault { get; set; }

            internal string InternalProperty { get; set; }

            public string PropertyWithPrivateSetter { get; private set; }
        }

        [Export]
        public class DefaultMetadataViewImporter
        {
            [Import]
            public Lazy<MetadataDecoratedPart, DefaultMetadataView> InnerPart { get; set; }
        }

        [Export]
        public class DerivedMetadataViewImporter
        {
            [Import]
            public Lazy<MetadataDecoratedPart, DerivedView> InnerPart { get; set; }

            public class DerivedView : DefaultMetadataView
            {
            }
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
        public class NoDefaultCtorMetadataViewImporter
        {
            [Import]
            public Lazy<MetadataDecoratedPart, DerivedView> InnerPart { get; set; }

            public class DerivedView : DefaultMetadataView
            {
#pragma warning disable SA1313 // Parameter names must begin with lower-case letter
                public DerivedView(string PublicProperty) { }
#pragma warning restore SA1313 // Parameter names must begin with lower-case letter
            }
        }

        [Export]
        public class PropertySetterThrowsMetadataViewImporter
        {
            [Import]
            public Lazy<MetadataDecoratedPart, PropertySetterThrowingMetadataView> InnerPart { get; set; }

            public class PropertySetterThrowingMetadataView
            {
                public string UnsettableProperty
                {
                    get => throw new NotImplementedException();
                    set => throw new InvalidOperationException();
                }
            }
        }

        [Export]
        [ExportMetadata(nameof(DefaultMetadataView.PublicProperty), PublicPropertyExpectedValue)]
        [ExportMetadata(nameof(DefaultMetadataView.InternalProperty), InternalPropertyExpectedValue)]
        [ExportMetadata(nameof(DefaultMetadataView.UnsettableProperty), "Any value")]
        [ExportMetadata(nameof(DefaultMetadataView.PropertyWithPrivateSetter), PublicPropertyExpectedValue)]
        [ExportMetadata("Some extra property", "Some value")]
        [ExportMetadata("AnotherExtraProperty", "Some value")]
        public class MetadataDecoratedPart
        {
        }
    }
}
