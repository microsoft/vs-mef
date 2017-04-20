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
    using MefV1 = System.ComponentModel.Composition;

    [Trait("IPartImportsSatisfiedNotification", "")]
    public class OnImportsSatisfiedTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SpecialPart), typeof(SomeRandomPart))]
        public void OnImportsSatisfied(IContainer container)
        {
            var part = container.GetExportedValue<SpecialPart>();
            Assert.Equal(1, part.ImportsSatisfiedInvocationCount);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(SpecialPartInternal), typeof(SomeRandomPart))]
        public void OnImportsSatisfiedInternalPart(IContainer container)
        {
            var part = container.GetExportedValue<SpecialPartInternal>();
            Assert.Equal(1, part.ImportsSatisfiedInvocationCount);
        }

        [MefV1.Export, Export]
        public class SpecialPart : MefV1.IPartImportsSatisfiedNotification
        {
            public int ImportsSatisfiedInvocationCount { get; set; }

            [Import, MefV1.Import]
            public SomeRandomPart SomeImport { get; set; }

            [OnImportsSatisfied] // V2
            public void ImportsSatisfied()
            {
                this.ImportsSatisfiedInvocationCount++;
                Assert.NotNull(this.SomeImport);
            }

            // V1. We're using explicit implementation syntax deliberately as part of the test.
            void MefV1.IPartImportsSatisfiedNotification.OnImportsSatisfied()
            {
                this.ImportsSatisfiedInvocationCount++;
                Assert.NotNull(this.SomeImport);
            }
        }

        [MefV1.Export]
        internal class SpecialPartInternal : MefV1.IPartImportsSatisfiedNotification
        {
            public int ImportsSatisfiedInvocationCount { get; set; }

            [MefV1.Import]
            public SomeRandomPart SomeImport { get; set; }

            // V1. We're using explicit implementation syntax deliberately as part of the test.
            void MefV1.IPartImportsSatisfiedNotification.OnImportsSatisfied()
            {
                this.ImportsSatisfiedInvocationCount++;
                Assert.NotNull(this.SomeImport);
            }
        }

        [MefV1.Export, Export]
        public class SomeRandomPart { }

        #region OnImportsSatisfiedInvokedAfterTransitiveImportsSatisfied

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V2Compat, typeof(RequestedPart), typeof(TransitivePart))]
        public void OnImportsSatisfiedInvokedAfterTransitiveImportsSatisfied(IContainer container)
        {
            bool invoked = false;
            TransitivePart.OnImportsSatisfiedHandler = transitive =>
            {
                Assert.NotNull(transitive.RequestedPart);
                Assert.Same(transitive, transitive.RequestedPart.TransitivePart);
                invoked = true;
            };
            var part = container.GetExportedValue<RequestedPart>();
            Assert.True(invoked);
        }

        [Export, Shared]
        [MefV1.Export]
        public class RequestedPart
        {
            [MefV1.Import, Import]
            public TransitivePart TransitivePart { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class TransitivePart : MefV1.IPartImportsSatisfiedNotification
        {
            [MefV1.Import, Import]
            public RequestedPart RequestedPart { get; set; }

            internal static Action<TransitivePart> OnImportsSatisfiedHandler;

            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                if (OnImportsSatisfiedHandler != null)
                {
                    OnImportsSatisfiedHandler(this);
                }
            }
        }

        #endregion

        #region Imperative query for part from within its own OnImportsSatisfied method

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartThatQueriesForItself))]
        public void PartQueriesForItselfInOnImportsSatisfied(IContainer container)
        {
            PartThatQueriesForItself.Container = container;

            var root = container.GetExportedValue<PartThatQueriesForItself>();
            Assert.NotNull(root);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatQueriesForItself : MefV1.IPartImportsSatisfiedNotification
        {
            internal static IContainer Container;
            private int onImportsSatisfiedInvocationCounter;

            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                Assert.Equal(1, ++this.onImportsSatisfiedInvocationCounter);
                var self = Container.GetExportedValue<PartThatQueriesForItself>();
                Assert.Same(this, self);
            }
        }

        #endregion
    }
}
