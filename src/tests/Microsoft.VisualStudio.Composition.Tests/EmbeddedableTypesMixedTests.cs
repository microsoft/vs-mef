// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using EmbeddedTypeReceiver;
    using Microsoft.VisualStudio.Composition.NonEmbeddingTypeReceiver;
    using Shell.Interop;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    /// <summary>
    /// Tests for support of embeddable types mixed with types that are not embedded (but could be elsewhere).
    /// </summary>
    public class EmbeddedableTypesMixedTests
    {
        [MefFact(CompositionEngines.V1Compat, "Microsoft.VisualStudio.Composition.NonEmbeddingTypeReceiver", typeof(EmbeddableTypesTests.PartThatExportsIVsProjectReference), SkipOnMono = true)]
        [Trait("NoPIA", "true")]
        public void EmbeddableTypeEmbeddedAndNotMixed(IContainer container)
        {
            var part = container.GetExportedValue<IExportedInterface>();
            Assert.NotNull(part);
        }
    }
}
