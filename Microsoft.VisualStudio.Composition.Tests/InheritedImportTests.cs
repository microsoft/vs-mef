namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class InheritedImportTests
    {
        [MefFact(CompositionEngines.V1Compat, typeof(DerivedClass), typeof(EmptyPart))]
        public void ClosedGenericBaseClassWithImportingField(IContainer container)
        {
            var derived = container.GetExportedValue<DerivedClass>();
            Assert.IsType<DerivedClass>(derived);
        }

        [MefV1.Export]
        public class EmptyPart { }

        [MefV1.Export]
        public class DerivedClass : BaseClass<object>
        {
        }

        public class BaseClass<T> 
        {
            /// <summary>
            /// An importing field.
            /// </summary>
            /// <remarks>
            /// It must be a field (not a property) for the test to verify what it is intended to.
            /// </remarks>
            [MefV1.Import]
            public EmptyPart ImportingField;
        }
    }
}
