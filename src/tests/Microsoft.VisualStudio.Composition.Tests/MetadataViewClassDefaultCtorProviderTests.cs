// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class MetadataViewClassDefaultCtorProviderTests
    {
        /* Tests to add:
         *   internal constructor on metadata view?
         *   internal properties on metadata view?
         *   metadata for which there is no matching properties?
         *   defaultValues?
         *   case sensitivity?
         *   exceptions thrown from setter?
         *   no default ctor, but has a constructor that takes some parameters.
         *   test properties without setters.
         */

        [MefFact(CompositionEngines.V2Compat, typeof(PartWithStronglyTypedMetadata), typeof(PartContainingPartWithStronglyTypedMetadata))]
        public void StronglyTypedMetadataOnType(IContainer container)
        {
            var part = container.GetExportedValue<PartContainingPartWithStronglyTypedMetadata>();
            Assert.NotNull(part);
            Assert.NotNull(part.InnerPart);
            Assert.Equal("MetadataPropertyValue", part.InnerPart.Metadata.Property);
        }

        [Export]
        public class PartContainingPartWithStronglyTypedMetadata
        {
            [Import]
            public Lazy<PartWithStronglyTypedMetadata, StronglyTypedMetadata> InnerPart { get; set; }
        }

        [Export]
        [StronglyTypedMetadata(Property = "MetadataPropertyValue")]
        public class PartWithStronglyTypedMetadata
        {
        }

        [MetadataAttribute]
        public class StronglyTypedMetadata : Attribute
        {
            public string Property { get; set; }
        }
    }
}
