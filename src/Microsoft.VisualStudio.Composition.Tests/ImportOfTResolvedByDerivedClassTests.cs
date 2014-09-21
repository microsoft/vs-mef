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

    [Trait("GenericExports", "Closed")]
    public class ImportOfTResolvedByDerivedClassTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void ImportOfTResolvedByDerivedClass(IContainer container)
        {
            var derived = container.GetExportedValue<DerivedClass>();
            Assert.NotNull(derived.ImportingProperty);

            Assert.NotNull(derived.ImportingCollections);
            Assert.NotNull(derived.ImportingCollections[0].Value);
        }

        public abstract class OpenGenericBaseClass<T>
        {
            [Import, MefV1.Import]
            public T ImportingProperty { get; set; }
            
            [ImportMany, MefV1.ImportMany]
            public Lazy<T, IDictionary<string, object>>[] ImportingCollections { get; set; }
        }

        [Export, MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class DerivedClass : OpenGenericBaseClass<Apple>
        {
        }

        [Export, MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class Apple { }
    }
}
