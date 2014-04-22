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

    public class EmbeddableTypesTests
    {
        /// <summary>
        /// Tests that Lazy{T} where T is an embeddable type works.
        /// </summary>
        /// <remarks>
        /// BUGBUG: The behavior we are testing for is broken in V2. It only works on V1.
        /// </remarks>
        [MefFact(
            CompositionEngines.V1/*Compat | CompositionEngines.V3EmulatingV2*/,
            "Microsoft.VisualStudio.Shell.Interop.12.0, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
            typeof(PartThatImportsLazyOfEmbeddedType),
            typeof(PartThatExportsEmbeddedType))]
        public void EmbeddedGenericTypeArgument(IContainer container)
        {
            var exporter = container.GetExportedValue<TEmbedded>();
            var importer = container.GetExportedValue<PartThatImportsLazyOfEmbeddedType>();
            Assert.Same(exporter, importer.RetargetProjectNoLazy);
        }

        [MefFact(
            CompositionEngines.V1/*Compat*/,
            "Microsoft.VisualStudio.Shell.Interop.12.0, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
            typeof(PartThatImportsLazyOfEmbeddedTypeNonPublic),
            typeof(PartThatExportsEmbeddedType))]
        public void EmbeddedGenericTypeArgumentNonPublicImportingProperty(IContainer container)
        {
            var exporter = container.GetExportedValue<TEmbedded>();
            var importer = container.GetExportedValue<PartThatImportsLazyOfEmbeddedTypeNonPublic>();
            Assert.Same(exporter, importer.RetargetProjectNoLazy);
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
    }
}
