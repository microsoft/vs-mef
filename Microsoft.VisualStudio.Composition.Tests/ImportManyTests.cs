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

    public class ImportManyTests
    {
        #region Array tests

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableArray))]
        public void ImportManyArrayWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableArray>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Length);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableArray), typeof(ExtensionOne))]
        public void ImportManyArrayWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableArray>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count());
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableArray), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyArrayWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableArray>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region IEnumerable<T> tests

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableIEnumerable))]
        public void ImportManyIEnumerableWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIEnumerable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableIEnumerable), typeof(ExtensionOne))]
        public void ImportManyIEnumerableWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIEnumerable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count());
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableIEnumerable), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyIEnumerableWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIEnumerable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region IList<T> tests

        [MefFact(CompositionEngines.V2Compat, typeof(ExtendableIList))]
        public void ImportManyIListWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(ExtendableIList), typeof(ExtensionOne))]
        public void ImportManyIListWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count());
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V2Compat, typeof(ExtendableIList), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyIListWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region List<T> tests

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableList))]
        public void ImportManyListWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableList), typeof(ExtensionOne))]
        public void ImportManyListWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count());
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableList), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyListWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        public interface IExtension { }

        [Export(typeof(IExtension))]
        [MefV1.Export(typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtensionOne : IExtension { }

        [Export(typeof(IExtension))]
        [MefV1.Export(typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtensionTwo : IExtension { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableIEnumerable
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IEnumerable<IExtension> Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableIList
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IList<IExtension> Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableList
        {
            [ImportMany]
            [MefV1.ImportMany]
            public List<IExtension> Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableArray
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IExtension[] Extensions { get; set; }
        }
    }
}
