// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using EmbeddedTypeReceiver;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class BaseGenericTypeTests
    {
        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(DerivedType))]
        public void GenericBaseTypeWithImportsTest(IContainer container)
        {
            var instance = container.GetExportedValue<DerivedType>();
            Assert.NotNull(instance.ImportingPropertyAccessor);
        }

        internal class GenericBaseType<T>
        {
            [MefV1.Import]
            protected PublicExport ImportingProperty { get; set; }
        }

        [MefV1.Export]
        internal class DerivedType : GenericBaseType<IList<IList<IFoo>>>
        {
            internal PublicExport ImportingPropertyAccessor
            {
                get { return this.ImportingProperty; }
            }
        }

        internal interface IFoo { }

        [MefV1.Export]
        public class PublicExport { }
    }
}
