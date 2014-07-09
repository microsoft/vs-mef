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

    public class NonSharedPartTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void ImportManyOfNonSharedExportsActivatesPartJustOnce(IContainer container)
        {
            var part = container.GetExportedValue<EnumerableImportManyOfNonSharedPart>();
            Assert.Same(part.NonSharedParts.Single(), part.NonSharedParts.Single());
            Assert.Same(part.LazyNonSharedParts.Single().Value, part.LazyNonSharedParts.Single().Value);
        }

        [Export, Shared]
        [MefV1.Export]
        public class EnumerableImportManyOfNonSharedPart
        {
            [ImportMany, MefV1.ImportMany]
            public IEnumerable<NonSharedPart> NonSharedParts { get; set; }

            [ImportMany, MefV1.ImportMany]
            public IEnumerable<Lazy<NonSharedPart>> LazyNonSharedParts { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPart { }
    }
}
