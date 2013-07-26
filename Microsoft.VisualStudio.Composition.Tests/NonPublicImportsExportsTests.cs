namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Access", "NonPublic")]
    public class NonPublicImportsExportsTests
    {
        [MefFact(CompositionEngines.V1Compat, typeof(InternalExport), typeof(PublicExport))]
        public void InternalExportedType(IContainer container)
        {
            var result = container.GetExportedValue<InternalExport>();
            Assert.NotNull(result);
            Assert.NotNull(result.InternalImportingProperty);
            Assert.NotNull(result.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalGenericExport<>), typeof(PublicExport))]
        public void InternalGenericExportedType(IContainer container)
        {
            var result = container.GetExportedValue<InternalGenericExport<IInternalInterface>>();
            Assert.NotNull(result);
            Assert.NotNull(result.InternalImportingProperty);
            Assert.NotNull(result.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExportOfInternalInterface), typeof(PublicExport))]
        public void PublicExportOfInternalType(IContainer container)
        {
            var result = container.GetExportedValue<IInternalInterface>();
            Assert.NotNull(result);
            Assert.IsType<PublicExportOfInternalInterface>(result);
            var cast = (PublicExportOfInternalInterface)result;
            Assert.NotNull(cast.InternalImportingProperty);
            Assert.NotNull(cast.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalTypeWithExportingMember))]
        public void GetExportsFromInternalTypeWithExportingMembers(IContainer container)
        {
            Assert.Equal(3, container.GetExportedValue<int>());
            Assert.Equal("Hi", container.GetExportedValue<string>());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalTypeWithExportingMember), typeof(PartThatImportsInternalMemberExports))]
        public void ImportFromInternalTypeWithExportingMembers(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsInternalMemberExports>();
            Assert.Equal(3, importer.ImportedInt);
            Assert.Equal("Hi", importer.ImportedString);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(ExportWithPrivateImportingProperty))]
        public void PrivateImportingProperty(IContainer container)
        {
            var result = container.GetExportedValue<ExportWithPrivateImportingProperty>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(OpenGenericPartWithPrivateImportingProperty<>))]
        public void PrivateImportingPropertyOnOpenGenericPart(IContainer container)
        {
            var result = container.GetExportedValue<OpenGenericPartWithPrivateImportingProperty<int>>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(ExportWithPrivateImportingField))]
        public void PrivateImportingField(IContainer container)
        {
            var result = container.GetExportedValue<ExportWithPrivateImportingField>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(OpenGenericPartWithPrivateImportingField<>))]
        public void PrivateImportingFieldOnOpenGenericPart(IContainer container)
        {
            var result = container.GetExportedValue<OpenGenericPartWithPrivateImportingField<int>>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingProperty))]
        public void PrivateExportingProperty(IContainer container)
        {
            string result = container.GetExportedValue<string>();
            Assert.Equal("Success", result);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingProperty), typeof(PartThatImportsPrivateExportingProperty))]
        public void ImportOfPrivateExportingProperty(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsPrivateExportingProperty>();
            Assert.Equal("Success", importer.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingField))]
        public void PrivateExportingField(IContainer container)
        {
            string result = container.GetExportedValue<string>();
            Assert.Equal("Success", result);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingField), typeof(PartThatImportsPrivateExportingField))]
        public void ImportOfPrivateExportingField(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsPrivateExportingField>();
            Assert.Equal("Success", importer.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportedMethod))]
        public void PrivateExportedMethod(IContainer container)
        {
            Func<string> result = container.GetExportedValue<Func<string>>();
            Assert.Equal("Success", result());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportedMethod), typeof(PartWithImportOfPrivateExportedMethod))]
        public void ImportOfPrivateExportedMethod(IContainer container)
        {
            var importer = container.GetExportedValue<PartWithImportOfPrivateExportedMethod>();
            Assert.Equal("Success", importer.Value());
        }

        internal interface IInternalInterface { }

        [MefV1.Export]
        internal class InternalExport
        {
            [MefV1.Import]
            public PublicExport PublicImportingProperty { get; set; }

            [MefV1.Import]
            internal PublicExport InternalImportingProperty { get; set; }
        }

        [MefV1.Export]
        internal class InternalGenericExport<T>
        {
            [MefV1.Import]
            public PublicExport PublicImportingProperty { get; set; }

            [MefV1.Import]
            internal PublicExport InternalImportingProperty { get; set; }
        }

        [MefV1.Export(typeof(IInternalInterface))]
        public class PublicExportOfInternalInterface : IInternalInterface
        {
            [MefV1.Import]
            public PublicExport PublicImportingProperty { get; set; }

            [MefV1.Import]
            internal PublicExport InternalImportingProperty { get; set; }
        }

        internal class InternalTypeWithExportingMember
        {
            [MefV1.Export]
            internal int InternalExportingInt
            {
                get { return 3; }
            }

            [MefV1.Export]
            public string PublicExportingString
            {
                get { return "Hi"; }
            }
        }

        [MefV1.Export]
        public class PartThatImportsInternalMemberExports
        {
            [MefV1.Import]
            public int ImportedInt { get; set; }

            [MefV1.Import]
            public string ImportedString { get; set; }
        }

        [MefV1.Export]
        public class PublicExport { }

        [MefV1.Export]
        public class ExportWithPrivateImportingProperty
        {
            [MefV1.Import]
            private PublicExport ImportingProperty { get; set; }

            internal PublicExport InternalAccessor
            {
                get { return this.ImportingProperty; }
            }
        }

        [MefV1.Export]
        public class OpenGenericPartWithPrivateImportingProperty<T>
        {
            [MefV1.Import]
            private PublicExport ImportingProperty { get; set; }

            internal PublicExport InternalAccessor
            {
                get { return this.ImportingProperty; }
            }
        }

        [MefV1.Export]
        public class ExportWithPrivateImportingField
        {
            [MefV1.Import]
            private PublicExport importingField = null;

            internal PublicExport InternalAccessor
            {
                get { return this.importingField; }
            }
        }

        [MefV1.Export]
        public class OpenGenericPartWithPrivateImportingField<T>
        {
            [MefV1.Import]
            private PublicExport importingField = null;

            internal PublicExport InternalAccessor
            {
                get { return this.importingField; }
            }
        }

        public class PartWithPrivateExportingProperty
        {
            [MefV1.Export]
            private string ExportingProperty
            {
                get { return "Success"; }
            }
        }

        [MefV1.Export]
        public class PartThatImportsPrivateExportingProperty
        {
            [MefV1.Import]
            public string Value { get; set; }
        }

        public class PartWithPrivateExportingField
        {
            [MefV1.Export]
            private string ExportingField = "Success";

            public string FieldAccessor
            {
                get { return this.ExportingField; }
            }
        }

        [MefV1.Export]
        public class PartThatImportsPrivateExportingField
        {
            [MefV1.Import]
            public string Value { get; set; }
        }

        public class PartWithPrivateExportedMethod
        {
            [MefV1.Export]
            private string GetValue()
            {
                return "Success";
            }
        }

        [MefV1.Export]
        public class PartWithImportOfPrivateExportedMethod
        {
            [MefV1.Import]
            public Func<string> Value { get; set; }
        }
    }
}
