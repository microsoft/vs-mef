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

    public class DynamicImportTests
    {
        [MefFact(CompositionEngines.V1Compat, typeof(Apple), typeof(PartImportingByDynamic))]
        public void AcquirePartWithDynamicImport(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingByDynamic>();
            Assert.IsType(typeof(Apple), part.Apple);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(Apple), typeof(PartImportingByObject))]
        public void AcquirePartWithObjectImport(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingByObject>();
            Assert.IsType(typeof(Apple), part.Apple);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(Apple), typeof(PartImportingByFruit), InvalidConfiguration = true)]
        public void AcquirePartWithBaseTypeImport(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingByFruit>();
        }

        [Export]
        [MefV1.Export]
        public class PartImportingByDynamic
        {
            [Import("SomeContract")]
            [MefV1.Import("SomeContract")]
            public dynamic Apple { get; set; }
        }

        [Export]
        [MefV1.Export]
        public class PartImportingByObject
        {
            [Import("SomeContract")]
            [MefV1.Import("SomeContract")]
            public object Apple { get; set; }
        }

        [Export]
        [MefV1.Export]
        public class PartImportingByFruit
        {
            [Import("SomeContract")]
            [MefV1.Import("SomeContract")]
            public Fruit Apple { get; set; }
        }

        public class Fruit { }

        [Export("SomeContract")]
        [MefV1.Export("SomeContract")]
        public class Apple : Fruit { }
    }
}
