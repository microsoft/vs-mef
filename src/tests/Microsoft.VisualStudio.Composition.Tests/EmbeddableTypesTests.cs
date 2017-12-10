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
    using Shell.Interop;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    // When these two using directives are switched in tandem with those
    // found in the PartThatImportsLazyOfEmbeddedType.cs source file,
    // one can verify that behaviors work with non-embeddable types
    // yet may break with embeddable ones.
    ////using TEmbedded = System.IDisposable;
    using TEmbedded = Microsoft.VisualStudio.Shell.Interop.IVsRetargetProjectAsync;

    /// <summary>
    /// Tests for support of embeddable types.
    /// </summary>
    /// <remarks>
    /// When it's time to add support for this feature,
    /// it may be done by generating code such as this:
    /// <code><![CDATA[
    /// var foo = typeof(ClassLibrary1.Class1).GetMethod("Foo");
    /// Type otherEmbedded = foo.GetParameters()[0].ParameterType.GetGenericArguments()[0];
    /// Type otherEmbedded = Type.GetType(otherEmbedded.AssemblyQualifiedName); // this *also* works
    /// var arg = typeof(Lazy<>).MakeGenericType(type).GetConstructor(new Type[0]).Invoke(new object[0]);
    /// ]]></code>
    /// The secret sauce here being that the instance of System.Type used to construct
    /// the Lazy`1 or LazyPart`1 instance is exactly taken from the assembly to which
    /// the value will be passed. That way, we'll get the instance of the Type that is
    /// embedded in that assembly and it will therefore be deemed compatible at runtime.
    /// </remarks>
    [Trait("NoPIA", "true")]
    [Trait(Traits.SkipOnMono, "NoPIA")]
    [Trait(Traits.SkipOnCoreCLR, "NoPIA")]
    public class EmbeddableTypesTests
    {
        /// <summary>
        /// Tests that Lazy{T} where T is an embeddable type works.
        /// </summary>
        /// <remarks>
        /// BUGBUG: The behavior we are testing for is broken in V2. It only works on V1.
        /// </remarks>
        [MefFact(
            CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2,
            typeof(PartThatImportsLazyOfEmbeddedType),
            typeof(PartThatExportsEmbeddedType))]
        public void EmbeddedGenericTypeArgument(IContainer container)
        {
            var exporter = container.GetExportedValue<TEmbedded>();
            var importer = container.GetExportedValue<PartThatImportsLazyOfEmbeddedType>();
            Assert.Same(exporter, importer.RetargetProjectNoLazy);
        }

        [MefFact(
            CompositionEngines.V1Compat,
            typeof(PartThatImportsLazyOfEmbeddedTypeNonPublic),
            typeof(PartThatExportsEmbeddedType))]
        public void EmbeddedGenericTypeArgumentNonPublicImportingProperty(IContainer container)
        {
            var exporter = container.GetExportedValue<TEmbedded>();
            var importer = container.GetExportedValue<PartThatImportsLazyOfEmbeddedTypeNonPublic>();
            Assert.Same(exporter, importer.RetargetProjectNoLazy);
        }

        [MefFact(
            CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2,
            typeof(PartThatImportsEmbeddedType),
            typeof(PartThatExportsEmbeddedType))]
        public void EmbeddedTypePublicImportingProperty(IContainer container)
        {
            var exporter = container.GetExportedValue<TEmbedded>();
            var importer = container.GetExportedValue<PartThatImportsEmbeddedType>();
            Assert.Same(exporter, importer.RetargetProject);
        }

        [MefFact(
            CompositionEngines.V1Compat,
            typeof(PartThatImportsExportFactoryOfEmbeddedTypeV1),
            typeof(PartThatExportsEmbeddedType))]
        public void EmbeddedTypePublicExportFactoryImportingPropertyV1(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportFactoryOfEmbeddedTypeV1>();
            Assert.NotNull(importer.CreateExport());
        }

        [MefFact(
            CompositionEngines.V3EmulatingV2,
            typeof(PartThatImportsExportFactoryOfEmbeddedTypeV2),
            typeof(PartThatExportsEmbeddedType))]
        public void EmbeddedTypePublicExportFactoryImportingPropertyV2(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportFactoryOfEmbeddedTypeV2>();
            Assert.NotNull(importer.CreateExport());
        }

        [Export(typeof(TEmbedded)), Shared]
        [MefV1.Export(typeof(TEmbedded))]
        public class PartThatExportsEmbeddedType : TEmbedded
        {
            public IVsTask CheckForRetargetAsync([ComAliasName("Microsoft.VisualStudio.Shell.Interop.RETARGET_CHECK_OPTIONS")]uint dwFlags)
            {
                throw new NotImplementedException();
            }

            public IVsTask GetAffectedFilesListAsync(IVsProjectTargetChange target)
            {
                throw new NotImplementedException();
            }

            public IVsTask RetargetAsync(IVsOutputWindowPane logger, [ComAliasName("Microsoft.VisualStudio.Shell.Interop.RETARGET_OPTIONS")]uint dwFlags, IVsProjectTargetChange target, [ComAliasName("OLE.LPCOLESTR")]string szProjectBackupLoaction)
            {
                throw new NotImplementedException();
            }

            public void Dispose() { }
        }

        #region Tests for embedded types with base types that are also embedded

        [MefFact(CompositionEngines.V1Compat, typeof(PartThatExportsIVsProjectReference))]
        public void EmbeddedTypeWithBaseEmbeddedType(IContainer container)
        {
            var part = container.GetExportedValue<PartThatExportsIVsProjectReference>();
            Assert.NotNull(part);
        }

        public class BaseClassForPartThatExportsIVsProjectReference<TInterface, TClass>
            where TInterface : IVsReference
            where TClass : TInterface
        {
        }

        [MefV1.Export]
        internal class PartThatExportsIVsProjectReference : BaseClassForPartThatExportsIVsProjectReference<IVsProjectReference, VsProjectReference>
        {
        }

        public class VsReferenceBase : IVsReference
        {
            public bool AlreadyReferenced { get; set; }

            public string FullPath { get; set; }

            public string Name { get; set; }
        }

        public class VsProjectReference : VsReferenceBase, IVsProjectReference
        {
            public string Identity { get; set; }

            public string ReferenceSpecification { get; set; }
        }

        #endregion
    }
}
