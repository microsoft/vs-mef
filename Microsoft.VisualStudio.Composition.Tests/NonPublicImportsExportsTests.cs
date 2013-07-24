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

        [MefFact(CompositionEngines.V1, typeof(PublicExport), typeof(ExportWithPrivateImportingProperty))]
        public void PrivateImportingProperty(IContainer container)
        {
            var result = container.GetExportedValue<ExportWithPrivateImportingProperty>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1, typeof(PublicExport), typeof(ExportWithPrivateImportingField))]
        public void PrivateImportingField(IContainer container)
        {
            var result = container.GetExportedValue<ExportWithPrivateImportingField>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithPrivateExportingProperty))]
        public void PrivateExportingProperty(IContainer container)
        {
            string result = container.GetExportedValue<string>();
            Assert.Equal("Success", result);
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithPrivateExportingField))]
        public void PrivateExportingField(IContainer container)
        {
            string result = container.GetExportedValue<string>();
            Assert.Equal("Success", result);
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithPrivateExportedMethod))]
        public void PrivateExportedMethod(IContainer container)
        {
            Func<string> result = container.GetExportedValue<Func<string>>();
            Assert.Equal("Success", result());
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
        public class ExportWithPrivateImportingField
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

        public class PartWithPrivateExportingField
        {
            [MefV1.Export]
            private string ExportingField = "Success";
        }

        public class PartWithPrivateExportedMethod
        {
            [MefV1.Export]
            private string GetValue()
            {
                return "Success";
            }
        }
    }
}
