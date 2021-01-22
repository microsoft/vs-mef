// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    public class NamedTypedExportTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Pear), typeof(Apple), typeof(FruitTree))]
        public void AcquireExportWithNamedImports(IContainer container)
        {
            FruitTree tree = container.GetExportedValue<FruitTree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Pear);
            Assert.IsAssignableFrom(typeof(Pear), tree.Pear);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Pear), typeof(Apple), typeof(FruitTree))]
        public void AcquireNamedExport(IContainer container)
        {
            Fruit fruit = container.GetExportedValue<Fruit>("Pear");
            Assert.NotNull(fruit);
            Assert.IsAssignableFrom(typeof(Pear), fruit);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Pear), typeof(Apple), typeof(FruitTree))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportsNamed(IContainer container)
        {
            IEnumerable<Lazy<Fruit>> result = container.GetExports<Fruit>("Pear");
            Assert.Equal(1, result.Count());
            Assert.IsType<Pear>(result.Single().Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(Apple))]
        public void AcquireExportWithDefaultContractName(IContainer container)
        {
            var fruit = container.GetExportedValue<Fruit>(typeof(Fruit).FullName);
            Assert.IsType(typeof(Apple), fruit);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(Apple))]
        public void AcquireExportWithEmptyContractName(IContainer container)
        {
            var fruit = container.GetExportedValue<Fruit>(string.Empty);
            Assert.IsType(typeof(Apple), fruit);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Apple))]
        public void AcquireExportWithNullContractName(IContainer container)
        {
            var fruit = container.GetExportedValue<Fruit>(null);
            Assert.IsType(typeof(Apple), fruit);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(Apple), typeof(AppleImportingPart))]
        public void ImportWithExplicitContractNameVariants(IContainer container)
        {
            var part = container.GetExportedValue<AppleImportingPart>();
            Assert.NotNull(part.AppleDefault);
            Assert.Same(part.AppleDefault, part.AppleEmptyString);
            Assert.Same(part.AppleDefault, part.AppleNull);
        }

        public class Fruit { }

        [Export("Pear", typeof(Fruit))]
        [MefV1.Export("Pear", typeof(Fruit))]
        public class Pear : Fruit { }

        [Export(typeof(Fruit)), Shared]
        [MefV1.Export(typeof(Fruit))]
        public class Apple : Fruit { }

        [Export]
        [MefV1.Export]
        public class FruitTree
        {
            [Import("Pear")]
            [MefV1.Import("Pear")]
            public Fruit Pear { get; set; } = null!;
        }

        [Export]
        [MefV1.Export]
        public class AppleImportingPart
        {
            [Import]
            [MefV1.Import]
            public Fruit? AppleDefault { get; set; }

            [Import(null)]
            [MefV1.Import((string?)null)]
            public Fruit? AppleNull { get; set; }

            [Import("")]
            [MefV1.Import("")]
            public Fruit? AppleEmptyString { get; set; }
        }

        #region Contract and type name collision testing

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SomeClass), typeof(SomeOtherExport))]
        public void ImportedContractNameCoincidesWithExportedTypeIdentity(IContainer container)
        {
            var part = container.GetExportedValue<SomeClass>();
            Assert.NotNull(part);
        }

        [MefV1.Export]
        [Export]
        public class SomeClass
        {
            [MefV1.ImportMany("SomeContractName")]
            [ImportMany("SomeContractName")]
            public IEnumerable<ISomeOtherInterface> ImportManyProperty { get; set; } = null!;

            [MefV1.Import("SomeContractName", AllowDefault = true)]
            [Import("SomeContractName", AllowDefault = true)]
            public ISomeOtherInterface? ImportProperty { get; set; }
        }

        [Export("SomeContractName")]
        public class SomeOtherExport { }

        public interface ISomeOtherInterface { }

        #endregion
    }
}
