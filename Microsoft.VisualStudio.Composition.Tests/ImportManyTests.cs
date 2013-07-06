namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ImportManyTests
    {
        [MefFact(CompositionEngines.V2Compat, typeof(Extendable))]
        public void ImportManyWithNone(IContainer container)
        {
            var extendable = container.GetExport<Extendable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }


        [MefFact(CompositionEngines.V2Compat, typeof(Extendable), typeof(ExtensionOne))]
        public void ImportManyWithOne(IContainer container)
        {
            var extendable = container.GetExport<Extendable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions[0]);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyWithTwo(IContainer container)
        {
            var extendable = container.GetExport<Extendable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        public interface IExtension { }

        [Export(typeof(IExtension))]
        public class ExtensionOne : IExtension { }

        [Export(typeof(IExtension))]
        public class ExtensionTwo : IExtension { }

        [Export]
        public class Extendable
        {
            [ImportMany]
            public IList<IExtension> Extensions { get; set; }
        }
    }
}
