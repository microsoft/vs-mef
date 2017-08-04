// Copyright (c) Microsoft. All rights reserved.

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
            Assert.NotNull(derived.ImportingField);
        }

        [MefFact(CompositionEngines.V1Compat)]
        public void ClosedGenericBaseClassWithImportingFieldSeveralDerivedTypes(IContainer container)
        {
            var derived = container.GetExportedValue<DerivedClass>();
            Assert.NotNull(derived.ImportingField);

            var derived2 = container.GetExportedValue<DerivedClass2>();
            Assert.NotNull(derived2.ImportingField);

            var derived3 = container.GetExportedValue<DerivedClass3>();
            Assert.NotNull(derived3.ImportingField);
        }

        [MefV1.Export]
        public class EmptyPart { }

        [MefV1.Export]
        public class DerivedClass : BaseClass<object>
        {
        }

        [MefV1.Export]
        public class DerivedClass2 : BaseClass<int>
        {
        }

        [MefV1.Export]
        public class DerivedClass3 : BaseClass<int>
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
