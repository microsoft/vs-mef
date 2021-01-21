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

    [Trait("Ambiguous", "PartName")]
    public class AmbiguousPartNamesTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Namespace1.SomePart), typeof(Namespace2.SomePart))]
        public void NonUniquePartTypeNames(IContainer container)
        {
            Assert.NotNull(container.GetExportedValue<Namespace1.SomePart>());
            Assert.NotNull(container.GetExportedValue<Namespace2.SomePart>());
        }

        public static class Namespace1
        {
            [Export]
            [MefV1.Export]
            public class SomePart { }
        }

        public static class Namespace2
        {
            [Export]
            [MefV1.Export]
            public class SomePart { }
        }
    }
}
