namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class OpenGenericExportTests
    {
        [MefFact(CompositionEngines.V2)]
        public void AcquireOpenGenericExport(IContainer container)
        {
            Useful<int> useful = container.GetExport<Useful<int>>();
            Assert.NotNull(useful);
        }

        [MefFact(CompositionEngines.V2)]
        public void AcquireExportWithImportOfOpenGenericExport(IContainer container)
        {
            User user = container.GetExport<User>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);
        }

        [Export]
        public class Useful<T> { }

        [Export]
        public class User
        {
            [Import]
            public Useful<int> Useful { get; set; }
        }
    }
}
