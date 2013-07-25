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
        [MefFact(CompositionEngines.V1, typeof(InternalExport))]
        public void InternalExportedType(IContainer container)
        {
            var result = container.GetExportedValue<InternalExport>();
            Assert.NotNull(result);
        }

        [MefFact(CompositionEngines.V1, typeof(PrivateExport))]
        public void PrivateExportedType(IContainer container)
        {
            var result = container.GetExportedValue<PrivateExport>();
            Assert.NotNull(result);
        }

        [MefFact(CompositionEngines.V1, typeof(PublicExportOfInternalInterface))]
        public void PublicExportOfInternalType(IContainer container)
        {
            var result = container.GetExportedValue<IInternalInterface>();
            Assert.NotNull(result);
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

        [MefFact(CompositionEngines.V1, typeof(PartWithPrivateExportingProperty), typeof(PartThatImportsPrivateExportingProperty))]
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

        [MefFact(CompositionEngines.V1, typeof(PartWithPrivateExportingField), typeof(PartThatImportsPrivateExportingField))]
        public void ImportOfPrivateExportingField(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsPrivateExportingField>();
            Assert.Equal("Success", importer.Value);
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithPrivateExportedMethod))]
        public void PrivateExportedMethod(IContainer container)
        {
            Func<string> result = container.GetExportedValue<Func<string>>();
            Assert.Equal("Success", result());
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithPrivateExportedMethod), typeof(PartWithImportOfPrivateExportedMethod))]
        public void ImportOfPrivateExportedMethod(IContainer container)
        {
            var importer = container.GetExportedValue<PartWithImportOfPrivateExportedMethod>();
            Assert.Equal("Success", importer.Value());
        }

        internal interface IInternalInterface { }

        [MefV1.Export]
        internal class InternalExport { }

        [MefV1.Export]
        private class PrivateExport { }

        [MefV1.Export(typeof(IInternalInterface))]
        public class PublicExportOfInternalInterface : IInternalInterface { }

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
            private PublicExport importingField;

            internal PublicExport InternalAccessor
            {
                get { return this.importingField; }
            }
        }

        [MefV1.Export]
        public class OpenGenericPartWithPrivateImportingField<T>
        {
            [MefV1.Import]
            private PublicExport importingField;

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
