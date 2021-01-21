// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.EmbeddedTypeReceiver;
    using Microsoft.VisualStudio.Composition.NonEmbeddingTypeReceiver;
    using Microsoft.VisualStudio.Shell.Interop;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    /// <summary>
    /// Tests for support of embeddable types mixed with types that are not embedded (but could be elsewhere).
    /// </summary>
    [Trait("NoPIA", "true")]
    [Trait(Traits.SkipOnMono, "NoPIA")]
    public class EmbeddedableTypesMixedTests
    {
        [MefFact(CompositionEngines.V1Compat, "Microsoft.VisualStudio.Composition.NonEmbeddingTypeReceiver", typeof(EmbeddableTypesTests.PartThatExportsIVsProjectReference))]
        public void EmbeddableTypeEmbeddedAndNotMixed(IContainer container)
        {
            var part = container.GetExportedValue<IExportedInterface>();
            Assert.NotNull(part);
        }
    }
}
