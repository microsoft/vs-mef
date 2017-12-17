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

    [Trait("Metadata", "CustomValue")]
    public class CustomMetadataValueTests
    {
        // Consider: do we want MEFv3 to follow V1's lead or V2's lead?
        // We could follow V2's lead by recognizing when we need to instantiate the attribute
        // at runtime in order to construct the metadata value.
        [MefFact(CompositionEngines.V1, typeof(ImportingPart), typeof(ExportWithCustomMetadata), InvalidConfiguration = true)]
        public void CustomMetadataValueV1(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPart>();
            Assert.IsType<MyObjectType>(importer.ImportingProperty.Metadata["SomeName"]);
            Assert.Equal(7, ((MyObjectType)importer.ImportingProperty.Metadata["SomeName"]).Value);
        }

#pragma warning disable CS1574
        /// <summary>
        /// Verifies that metadata values can be complex, non-serializable objects.
        /// </summary>
        /// <remarks>
        /// When it comes time to support this in V3, <see cref="ReflectionHelpers.Instantiate(CustomAttributeData)"/>
        /// is expected to come in useful. We can cache that CustomAttributeData the same way we cache other reflection
        /// data, and use it to reconstitute the value at runtime.
        /// </remarks>
#pragma warning restore CS1574
        [MefFact(CompositionEngines.V2, NoCompatGoal = true)]
        public void CustomMetadataValueV2(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPart>();
            Assert.IsType<MyObjectType>(importer.ImportingProperty.Metadata["SomeName"]);
            Assert.Equal(7, ((MyObjectType)importer.ImportingProperty.Metadata["SomeName"]).Value);
        }

        [Export]
        [MefV1.Export]
        public class ImportingPart
        {
            [Import]
            [MefV1.Import]
            public Lazy<ExportWithCustomMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [Export]
        [MefV1.Export]
        [CustomMetadata(1, Field = 2, Property = 4)]
        public class ExportWithCustomMetadata { }

        [MetadataAttribute]
        [MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        public class CustomMetadataAttribute : Attribute
        {
            private int positional;

            public CustomMetadataAttribute(int positional)
            {
                this.positional = positional;
            }

            public MyObjectType SomeName
            {
                get { return new MyObjectType(this.positional + this.Field + this.Property); }
            }

            public int Field;

            public int Property { get; set; }
        }

        /// <summary>
        /// A intentionally non-serializable object.
        /// </summary>
        public class MyObjectType
        {
            internal MyObjectType(int value)
            {
                this.Value = value;
            }

            public int Value { get; private set; }
        }
    }
}
