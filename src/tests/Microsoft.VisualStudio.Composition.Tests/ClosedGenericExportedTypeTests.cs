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

    /// <summary>
    /// MEFv1 and MEFv2 don't support closed generic type exports of MEF parts.
    /// At the moment, MEFv3 supports it. If it becomes a problem however, we can surrender such support
    /// if it means it makes something else better.
    /// </summary>
    [Trait("GenericExports", "Closed")]
    public class ClosedGenericExportedTypeTests
    {
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2)]
        public void ImportOfClosedGenericExportedType(IContainer container)
        {
            var appleTree = container.GetExportedValue<Tree<Apple>>();
            Assert.NotNull(appleTree);
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2)]
        public void ImportOfClosedGenericExportedTypeWithWrongTypeArg(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<Tree<Pear>>());
        }

        [Export(typeof(Tree<Apple>)), Shared]
        [MefV1.Export(typeof(Tree<Apple>))]
        public class Tree<T>
        {
            public List<T>? Fruit { get; set; }
        }

        public class Apple { }

        public class Pear { }
    }
}
