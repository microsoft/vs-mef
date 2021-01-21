// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Static", "")]
    public class StaticMemberExportsTests
    {
        [MefFact(CompositionEngines.V1Compat, typeof(StaticPartWithStaticExports), typeof(ImportingPart))]
        public void ExportingStaticPartStaticProperty(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal("Hello", part.ImportOfProperty);

            // These stay null because the exports for these values are not included in the test.
            Assert.Null(part.ImportOfField);
            Assert.Null(part.ImportOfMethod);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithStaticExports), typeof(ImportingPart))]
        public void ExportingStaticMembers(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal(PartWithStaticExports.ExportingProperty, part.ImportOfProperty);
            Assert.Equal(PartWithStaticExports.ExportingField, part.ImportOfField);
            Assert.Equal(PartWithStaticExports.ExportingMethod(), part.ImportOfMethod());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithStaticExports), typeof(ImportingPart))]
        public void GetExportsOfExportingStaticMembers(IContainer container)
        {
            Assert.Equal(PartWithStaticExports.ExportingProperty, container.GetExportedValue<string>("Property"));
            Assert.Equal(PartWithStaticExports.ExportingField, container.GetExportedValue<string>("Field"));
            Assert.Equal(PartWithStaticExports.ExportingMethod(), container.GetExportedValue<Func<string>>("Method")());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithStaticExports), typeof(ImportManyWithMetadataPart))]
        public void ImportManyMetadataStaticPropertyExport(IContainer container)
        {
            var part = container.GetExportedValue<ImportManyWithMetadataPart>();
            Assert.Equal(1, part.ImportingMember.Count);
            Assert.Equal("SomeValue", part.ImportingMember[0].Metadata["SomeName"]);
            Assert.Equal(PartWithStaticExports.ExportingProperty, part.ImportingMember[0].Value);
        }

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(UninstantiablePartWithMetadataOnExports), typeof(PartImportingNonInstantiablePartExports))]
        public void NonInstantiablePartStillHasExportMetadata(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingNonInstantiablePartExports>();
            Assert.Equal("SomeValue", part.FieldImportingMember.Metadata["SomeName"]);
            Assert.Equal("SomeValue", part.PropertyImportingMember.Metadata["SomeName"]);

            Assert.Equal("Hello", part.FieldImportingMember.Value);

            // Instance member requires an importing constructor on the exporting part.
            Assert.Throws<CompositionFailedException>(() => part.PropertyImportingMember.Value);
        }

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(InternalUninstantiablePartWithMetadataOnExports), typeof(PartImportingNonInstantiablePartExports))]
        public void InternalNonInstantiablePartStillHasExportMetadata(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingNonInstantiablePartExports>();
            Assert.Equal("SomeValue", part.FieldImportingMember.Metadata["SomeName"]);
            Assert.Equal("SomeValue", part.PropertyImportingMember.Metadata["SomeName"]);

            Assert.Equal("Hello", part.FieldImportingMember.Value);

            // Instance member requires an importing constructor on the exporting part.
            Assert.Throws<CompositionFailedException>(() => part.PropertyImportingMember.Value);
        }

        [MefFact(CompositionEngines.V1, typeof(UninstantiablePartWithMetadataOnExports), typeof(PartImportingNonInstantiablePartExports), NoCompatGoal = true)]
        public void NonInstantiablePartStillHasExportMetadataV1(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingNonInstantiablePartExports>();
            Assert.Equal("SomeValue", part.FieldImportingMember.Metadata["SomeName"]);
            Assert.Equal("SomeValue", part.PropertyImportingMember.Metadata["SomeName"]);

            // Static member doesn't strictly require an importing constructor to retrieve,
            // but MEFv1 requires it anyway.
            Assert.Throws<MefV1.CompositionException>(() => part.FieldImportingMember.Value);

            // Instance member requires an importing constructor on the exporting part.
            Assert.Throws<MefV1.CompositionException>(() => part.PropertyImportingMember.Value);
        }

        public class PartWithStaticExports
        {
            [MefV1.Export("Property")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string ExportingProperty
            {
                get { return "Hello"; }
            }

            [MefV1.Export("Field")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string ExportingField = "Hello";

            [MefV1.Export("Method")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string ExportingMethod() { return "Hello"; }
        }

        public static class StaticPartWithStaticExports
        {
            [MefV1.Export("Property")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string ExportingMember
            {
                get { return "Hello"; }
            }
        }

        public class UninstantiablePartWithMetadataOnExports
        {
            [MefV1.Export("Field")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string StaticExportingField = "Hello";

            [MefV1.Export("Property")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public string InstanceExportingProperty
            {
                get { return "Hello"; }
            }

            public UninstantiablePartWithMetadataOnExports(object someValue)
            {
                // This constructor suppresses the construction of the default constructor.
                // It is deliberately *not* an ImportingConstructor because this class
                // is for a test that metadata on exports is still available even when
                // the exported value itself cannot be obtained (for instance exports).
            }
        }

        internal class InternalUninstantiablePartWithMetadataOnExports
        {
            [MefV1.Export("Field")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string StaticExportingField = "Hello";

            [MefV1.Export("Property")]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public string InstanceExportingProperty
            {
                get { return "Hello"; }
            }

            public InternalUninstantiablePartWithMetadataOnExports(object someValue)
            {
                // This constructor suppresses the construction of the default constructor.
                // It is deliberately *not* an ImportingConstructor because this class
                // is for a test that metadata on exports is still available even when
                // the exported value itself cannot be obtained (for instance exports).
            }
        }

        [MefV1.Export]
        public class PartImportingNonInstantiablePartExports
        {
            [MefV1.Import("Field")]
            public Lazy<string, IDictionary<string, object>> FieldImportingMember { get; set; } = null!;

            [MefV1.Import("Property")]
            public Lazy<string, IDictionary<string, object>> PropertyImportingMember { get; set; } = null!;
        }

        [MefV1.Export]
        public class ImportingPart
        {
            [MefV1.Import("Property")]
            public string ImportOfProperty { get; set; } = null!;

            [MefV1.Import("Field", AllowDefault = true)]
            public string ImportOfField { get; set; } = null!;

            [MefV1.Import("Method", AllowDefault = true)]
            public Func<string> ImportOfMethod { get; set; } = null!;
        }

        [MefV1.Export]
        public class ImportManyWithMetadataPart
        {
            [MefV1.ImportMany("Property")]
            public List<Lazy<string, IDictionary<string, object>>> ImportingMember { get; set; } = null!;
        }
    }
}
