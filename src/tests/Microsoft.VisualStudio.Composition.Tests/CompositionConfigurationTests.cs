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
    using MEFv1 = System.ComponentModel.Composition;

    public class CompositionConfigurationTests
    {
        [Fact]
        public void Create()
        {
            Assert.Throws<ArgumentNullException>(() => ComposableCatalog.Create(null!));

            Assert.NotNull(ComposableCatalog.Create(Resolver.DefaultInstance));
        }
    }
}
