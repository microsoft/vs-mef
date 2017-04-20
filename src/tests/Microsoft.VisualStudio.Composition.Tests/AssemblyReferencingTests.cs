// Copyright (c) Microsoft. All rights reserved.

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
            public Lazy<SomeExport, ISomeInterface> SomeImport { get; set; }
        }

        [Export, MefV1.Export]
        public class SomeExport { }
    }
}
