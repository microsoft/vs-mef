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
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class AssemblyReferencingTests
    {
        [MefFact(CompositionEngines.V1Compat)]
        public void CodeGenReferencesBaseClassAssembly(IContainer container)
        {
            var export = container.GetExportedValue<DiscoverablePartDerived>();
            Assert.NotNull(export);
        }

        [Export, MefV1.Export]
        public class DiscoverablePartDerived
        {
            [Import]
            [MefV1.Import]
            public Lazy<SomeExport, ISomeInterface> SomeImport { get; set; } = null!;
        }

        [Export, MefV1.Export]
        public class SomeExport { }
    }
}
