// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Composition;
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

        /// <summary>
        /// Documents that MEF v1 tended to call OnImportsSatisfied in the order of its dependency stack.
        /// </summary>
        /// <remarks>
        /// This would break down for Lazy imports and circular dependencies but in the simplest case it seems reliable,
        /// so we document the simple case, but we will not strive to emulate it because it's a very limited case,
        /// undocumented behavior, and would be difficult to emulate while maintaining our own support for circular dependencies
        /// which is broader than .NET MEF's.
        /// </remarks>
        [MefFact(CompositionEngines.V1, typeof(OuterPart), typeof(InnerPart), NoCompatGoal = true)]
        public void OnImportsSatisfiedInDependencyOrder(IContainer container)
        {
            var part = container.GetExportedValue<OuterPart>();
            Assert.True(part.OnImportsSatisfiedCalled);
            Assert.True(part.InnerPart.OnImportsSatisfiedCalled);
            Assert.True(part.OnImportsSatisfiedCalledOnInnerPart);  // Verify that the OnImportSatisfied() was called on the inner part first
        }

        [MefV1.Export, Export]
        public class OuterPart : MefV1.IPartImportsSatisfiedNotification
        {
            public bool OnImportsSatisfiedCalled { get; private set; }

            public bool OnImportsSatisfiedCalledOnInnerPart { get; private set; }

            [MefV1.Import, Import]
            public InnerPart InnerPart { get; set; } = null!;

            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                this.OnImportsSatisfiedCalled = true;
                this.OnImportsSatisfiedCalledOnInnerPart = this.InnerPart.OnImportsSatisfiedCalled;
            }
        }

        [MefV1.Export, Export]
        public class InnerPart : MefV1.IPartImportsSatisfiedNotification
        {
            public bool OnImportsSatisfiedCalled { get; private set; }

            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                this.OnImportsSatisfiedCalled = true;
            }
        }

        [MefV1.Export, Export]
        public class SpecialPart : MefV1.IPartImportsSatisfiedNotification
        {
            public int ImportsSatisfiedInvocationCount { get; set; }

            [Import, MefV1.Import]
            public SomeRandomPart SomeImport { get; set; } = null!;

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
            public SomeRandomPart SomeImport { get; set; } = null!;

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
            public TransitivePart TransitivePart { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class TransitivePart : MefV1.IPartImportsSatisfiedNotification
        {
            [MefV1.Import, Import]
            public RequestedPart RequestedPart { get; set; } = null!;

            internal static Action<TransitivePart>? OnImportsSatisfiedHandler;

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
            internal static IContainer? Container;
            private int onImportsSatisfiedInvocationCounter;

            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                Assert.Equal(1, ++this.onImportsSatisfiedInvocationCounter);
                Assumes.NotNull(Container);
                var self = Container.GetExportedValue<PartThatQueriesForItself>();
                Assert.Same(this, self);
            }
        }

        #endregion

        #region Non-public OnImportsSatisfied methods

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V2Compat, typeof(ClassWithInternalOnImportsSatisfiedMethod))]
        public void InternalOnImportsSatisfiedMethodIsObserved(IContainer container)
        {
            var export = container.GetExportedValue<ClassWithInternalOnImportsSatisfiedMethod>();
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V2Compat, typeof(ClassWithInternalOnImportsSatisfiedMethodDerived))]
        public void InternalOnImportsSatisfiedMethodOnBaseIsObserved(IContainer container)
        {
            var export = container.GetExportedValue<ClassWithInternalOnImportsSatisfiedMethodDerived>();
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V3EmulatingV2WithNonPublic, typeof(ClassWithPrivateOnImportsSatisfiedMethod))]
        public void PrivateOnImportsSatisfiedMethodIsObserved(IContainer container)
        {
            var export = container.GetExportedValue<ClassWithPrivateOnImportsSatisfiedMethod>();
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V3EmulatingV2WithNonPublic, typeof(ClassWithPrivateOnImportsSatisfiedMethodDerived), Skip = "Not yet passing.")]
        public void PrivateOnImportsSatisfiedMethodOnBaseIsObserved(IContainer container)
        {
            var export = container.GetExportedValue<ClassWithPrivateOnImportsSatisfiedMethodDerived>();
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount);
        }

        [Export]
        internal class ClassWithInternalOnImportsSatisfiedMethod
        {
            internal int ImportsSatisfiedMethodRunCount { get; private set; }

            [OnImportsSatisfied]
            internal void PrivateImportsSatisfiedMethod()
            {
                this.ImportsSatisfiedMethodRunCount++;
            }
        }

        [Export]
        internal class ClassWithInternalOnImportsSatisfiedMethodDerived : ClassWithInternalOnImportsSatisfiedMethod
        {
        }

        [Export]
        internal class ClassWithPrivateOnImportsSatisfiedMethod
        {
            internal int ImportsSatisfiedMethodRunCount { get; private set; }

            [OnImportsSatisfied]
            private void PrivateImportsSatisfiedMethod()
            {
                this.ImportsSatisfiedMethodRunCount++;
            }
        }

        [Export]
        internal class ClassWithPrivateOnImportsSatisfiedMethodDerived : ClassWithPrivateOnImportsSatisfiedMethod
        {
        }

        #endregion

        #region Test for attributes applied to multiple methods

        [MefFact(CompositionEngines.V2Compat, typeof(ClassWithTwoOnImportsSatisfiedMethods))]
        public void TwoOnImportsSatisfiedMethodAreObserved(IContainer container)
        {
            var export = container.GetExportedValue<ClassWithTwoOnImportsSatisfiedMethods>();
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount1);
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount2);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(DerivedClassWithTwoMoreOnImportsSatisfiedMethods))]
        public void OnImportsSatisfiedMethodAreObservedAcrossTypeHierarchy(IContainer container)
        {
            var export = container.GetExportedValue<DerivedClassWithTwoMoreOnImportsSatisfiedMethods>();
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount1);
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount2);
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount3);
            Assert.Equal(1, export.ImportsSatisfiedMethodRunCount4);
        }

        [Export]
        internal class ClassWithTwoOnImportsSatisfiedMethods
        {
            internal int ImportsSatisfiedMethodRunCount1 { get; private set; }

            internal int ImportsSatisfiedMethodRunCount2 { get; private set; }

            [OnImportsSatisfied]
            internal void InternalImportsSatisfiedMethod1() => this.ImportsSatisfiedMethodRunCount1++;

            [OnImportsSatisfied]
            public void PublicImportsSatisfiedMethod2() => this.ImportsSatisfiedMethodRunCount2++;
        }

        [Export]
        internal class DerivedClassWithTwoMoreOnImportsSatisfiedMethods : ClassWithTwoOnImportsSatisfiedMethods
        {
            internal int ImportsSatisfiedMethodRunCount3 { get; private set; }

            internal int ImportsSatisfiedMethodRunCount4 { get; private set; }

            [OnImportsSatisfied]
            internal void InternalImportsSatisfiedMethod3() => this.ImportsSatisfiedMethodRunCount3++;

            [OnImportsSatisfied]
            public void PublicImportsSatisfiedMethod4() => this.ImportsSatisfiedMethodRunCount4++;
        }

        #endregion
    }
}
