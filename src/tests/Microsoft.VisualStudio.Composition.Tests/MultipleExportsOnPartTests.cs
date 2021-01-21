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

    public class MultipleExportsOnPartTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void MultipleExportsOnSharedPart(IContainer container)
        {
            SharedExport sharedExport = container.GetExportedValue<SharedExport>();
            object sharedExport2 = container.GetExportedValue<object>("SharedExport");
            Assert.Same(sharedExport, sharedExport2);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void MultipleExportsOnNonSharedPart(IContainer container)
        {
            NonSharedExport nonSharedExport = container.GetExportedValue<NonSharedExport>();
            object nonSharedExport2 = container.GetExportedValue<object>("NonSharedExport");
            Assert.NotSame(nonSharedExport, nonSharedExport2);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void MultipleImportsOfNonSharedPart(IContainer container)
        {
            var importer = container.GetExportedValue<MultipleImportsForSamePart>();
            Assert.NotSame(importer.NonSharedExport1, importer.NonSharedExport2);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void MultipleImportsOfSharedPart(IContainer container)
        {
            var importer = container.GetExportedValue<MultipleImportsForSamePart>();
            Assert.Same(importer.SharedExport1, importer.SharedExport2);
        }

        [Export, Export("NonSharedExport", typeof(object))]
        [MefV1.Export, MefV1.Export("NonSharedExport", typeof(object)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedExport { }

        [Export, Export("SharedExport", typeof(object)), Shared]
        [MefV1.Export, MefV1.Export("SharedExport", typeof(object))]
        public class SharedExport { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class MultipleImportsForSamePart
        {
            [Import, MefV1.Import]
            public NonSharedExport NonSharedExport1 { get; set; } = null!;

            [Import("NonSharedExport"), MefV1.Import("NonSharedExport")]
            public object NonSharedExport2 { get; set; } = null!;

            [Import, MefV1.Import]
            public SharedExport SharedExport1 { get; set; } = null!;

            [Import("SharedExport"), MefV1.Import("SharedExport")]
            public object SharedExport2 { get; set; } = null!;
        }
    }
}
