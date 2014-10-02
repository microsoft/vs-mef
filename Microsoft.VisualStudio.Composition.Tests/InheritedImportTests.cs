namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class InheritedImportTests
    {
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3SkipCodeGenScenario, typeof(DerivedClass), typeof(ClassToImport))]
        public void InheritedImportOnGenericBaseTypeWorks(IContainer container)
        {
            var derived = container.GetExportedValue<DerivedClass>();
            Assert.IsType<DerivedClass>(derived);
        }

        [MefV1.Export]
        public class ClassToImport { }

        [MefV1.Export]
        public class DerivedClass : BaseClass<object>
        {
        }

        public abstract class BaseClass<T> 
        {
            [MefV1.Import]
            private ClassToImport Import = null;
        }
    }
}
