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
            Assert.Single(part.ImportingMember);
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

        [MefFact(CompositionEngines.V1Compat, typeof(ClassWithStaticMemberExports))]
        public void StaticFieldExportDoesNotInstantiateClass(IContainer container)
        {
            ClassWithStaticMemberExports.ConstructorCalled = false;

            string value = container.GetExports<string>("StaticField").Single().Value;

            Assert.Equal("StaticFieldValue", value);
            Assert.False(ClassWithStaticMemberExports.ConstructorCalled);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ClassWithStaticMemberExports))]
        public void StaticPropertyExportDoesNotInstantiateClass(IContainer container)
        {
            ClassWithStaticMemberExports.ConstructorCalled = false;

            string value = container.GetExports<string>("StaticProperty").Single().Value;

            Assert.Equal("StaticPropertyValue", value);
            Assert.False(ClassWithStaticMemberExports.ConstructorCalled);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ClassWithStaticMemberExports))]
        public void StaticMethodExportDoesNotInstantiateClass(IContainer container)
        {
            ClassWithStaticMemberExports.ConstructorCalled = false;

            Func<string> value = container.GetExports<Func<string>>("StaticMethod").Single().Value;

            Assert.Equal("StaticMethodValue", value());
            Assert.False(ClassWithStaticMemberExports.ConstructorCalled);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ClassWithStaticMemberExports), typeof(PartImportingStaticProperty))]
        public void StaticMemberImportDoesNotInstantiateClass(IContainer container)
        {
            ClassWithStaticMemberExports.ConstructorCalled = false;

            var importingPart = container.GetExportedValue<PartImportingStaticProperty>();

            Assert.Equal("StaticPropertyValue", importingPart.Value);
            Assert.False(ClassWithStaticMemberExports.ConstructorCalled);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ClassWithStaticMemberExports), typeof(PartImportingStaticPropertyFactory))]
        public void ExportFactoryOfStaticMemberDoesNotInstantiateClass(IContainer container)
        {
            ClassWithStaticMemberExports.ConstructorCalled = false;
            var importingPart = container.GetExportedValue<PartImportingStaticPropertyFactory>();

            using MefV1.ExportLifetimeContext<string> export = importingPart.Factory.CreateExport();

            Assert.Equal("StaticPropertyValue", export.Value);
            Assert.False(ClassWithStaticMemberExports.ConstructorCalled);
        }

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ClassWithStaticMemberExports), typeof(PartImportingStaticPropertyFactory), NoCompatGoal = true)]
        public void ExportFactoryOfStaticMemberThrowsAfterContainerDisposal(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartImportingStaticPropertyFactory>();
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() => importingPart.Factory.CreateExport());
        }

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ClassWithStaticDisposableMemberExports), NoCompatGoal = true)]
        public void ExportFactoryOfStaticMemberDoesNotDisposeExportedValue(IContainer container)
        {
            ClassWithStaticDisposableMemberExports.ExportedValue.Disposed = false;
            var v3Container = (TestUtilities.V3ContainerWrapper)container;
            using var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var importingPart = new PartImportingStaticDisposableMemberFactory();
            MefV1.AttributedModelServices.SatisfyImportsOnce(v1Container, importingPart);

            MefV1.ExportLifetimeContext<DisposableExportedValue> export = importingPart.Factory.CreateExport();
            export.Dispose();

            Assert.False(ClassWithStaticDisposableMemberExports.ExportedValue.Disposed);
        }

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ClassWithStaticMemberExports), typeof(PartImportingLazyStaticProperty), NoCompatGoal = true)]
        public void LazyStaticMemberImportThrowsAfterContainerDisposal(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartImportingLazyStaticProperty>();
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() => importingPart.Value.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ClassWithMixedExports))]
        public void InstanceMemberExportInstantiatesClass(IContainer container)
        {
            ClassWithMixedExports.ConstructorCalled = false;

            string value = container.GetExports<string>("InstanceProperty").Single().Value;

            Assert.Equal("InstanceValue", value);
            Assert.True(ClassWithMixedExports.ConstructorCalled);
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

        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ClassWithStaticMemberExports
        {
            public static bool ConstructorCalled { get; set; }

            public ClassWithStaticMemberExports()
            {
                ConstructorCalled = true;
            }

            [MefV1.Export("StaticField")]
            public static string StaticField = "StaticFieldValue";

            [MefV1.Export("StaticProperty")]
            public static string StaticProperty => "StaticPropertyValue";

            [MefV1.Export("StaticMethod")]
            public static string StaticMethod() => "StaticMethodValue";
        }

        [MefV1.Export]
        public class PartImportingStaticProperty
        {
            [MefV1.Import("StaticProperty")]
            public string Value { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartImportingStaticPropertyFactory
        {
            [MefV1.Import("StaticProperty")]
            public MefV1.ExportFactory<string> Factory { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartImportingLazyStaticProperty
        {
            [MefV1.Import("StaticProperty")]
            public Lazy<string> Value { get; set; } = null!;
        }

        public class PartImportingStaticDisposableMemberFactory
        {
            [MefV1.Import]
            public MefV1.ExportFactory<DisposableExportedValue> Factory { get; set; } = null!;
        }

        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ClassWithStaticDisposableMemberExports
        {
            [MefV1.Export]
            public static DisposableExportedValue ExportedValue { get; } = new DisposableExportedValue();
        }

        public class DisposableExportedValue : IDisposable
        {
            public bool Disposed { get; set; }

            public void Dispose()
            {
                this.Disposed = true;
            }
        }

        public class ClassWithMixedExports
        {
            public static bool ConstructorCalled { get; set; }

            public ClassWithMixedExports()
            {
                ConstructorCalled = true;
            }

            [MefV1.Export("InstanceProperty")]
            public string InstanceProperty => "InstanceValue";
        }
    }
}
