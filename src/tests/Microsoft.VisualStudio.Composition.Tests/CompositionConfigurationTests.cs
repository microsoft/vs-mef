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
    using MEFv1 = System.ComponentModel.Composition;

    public class CompositionConfigurationTests
    {
        [Fact]
        public void Create()
        {
            Assert.Throws<ArgumentNullException>(() => ComposableCatalog.Create(null));

            Assert.NotNull(ComposableCatalog.Create(Resolver.DefaultInstance));
        }
    }
}
