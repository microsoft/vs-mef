// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.EmbeddedTypeReceiver;
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
            protected PublicExport ImportingProperty { get; set; } = null!;
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
