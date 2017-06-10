// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.EmbeddedTypeReceiver
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Shell.Interop;
#if DESKTOP
    using MefV1 = System.ComponentModel.Composition;
#endif
    using MefV2 = System.Composition;

    // When these two using directives are switched in tandem with those
    // found in the PartThatImportsLazyOfEmbeddedType.cs source file,
    // one can verify that behaviors work with non-embeddable types
    // yet may break with embeddable ones.
    ////using TEmbedded = System.IDisposable;
    using TEmbedded = Microsoft.VisualStudio.Shell.Interop.IVsRetargetProjectAsync;

    /// <summary>
    /// The type must appear in a different assembly from the exporting part
    /// so that the two assemblies have distinct Type instances for the embeddable interface.
    /// </summary>
#if DESKTOP
    [MefV1.Export]
#endif
    [MefV2.Export]
    public class PartThatImportsLazyOfEmbeddedType
    {
#if DESKTOP
        [MefV1.Import]
#endif
        [MefV2.Import]
        public Lazy<TEmbedded> RetargetProject { get; set; }

        public TEmbedded RetargetProjectNoLazy
        {
            get { return this.RetargetProject.Value; }
        }
    }

#if DESKTOP
    [MefV1.Export]
#endif
    [MefV2.Export]
    public class PartThatImportsLazyOfEmbeddedTypeNonPublic
    {
        public TEmbedded RetargetProjectNoLazy
        {
            get { return this.RetargetProject.Value; }
        }

#if DESKTOP
        [MefV1.Import]
#endif
        [MefV2.Import]
        internal Lazy<TEmbedded> RetargetProject { get; set; }
    }

#if DESKTOP
    [MefV1.Export]
#endif
    [MefV2.Export]
    public class PartThatImportsEmbeddedType
    {
#if DESKTOP
        [MefV1.Import]
#endif
        [MefV2.Import]
        public TEmbedded RetargetProject { get; set; }
    }

#if DESKTOP
    /// <summary>
    /// The type must appear in a different assembly from the exporting part
    /// so that the two assemblies have distinct Type instances for the embeddable interface.
    /// </summary>
    [MefV1.Export]
    public class PartThatImportsExportFactoryOfEmbeddedTypeV1
    {
        [MefV1.Import]
        public MefV1.ExportFactory<TEmbedded> RetargetProjectFactory { get; set; }

        public TEmbedded CreateExport()
        {
            return this.RetargetProjectFactory.CreateExport().Value;
        }
    }
#endif

    /// <summary>
    /// The type must appear in a different assembly from the exporting part
    /// so that the two assemblies have distinct Type instances for the embeddable interface.
    /// </summary>
    [MefV2.Export]
    public class PartThatImportsExportFactoryOfEmbeddedTypeV2
    {
        [MefV2.Import]
        public MefV2.ExportFactory<TEmbedded> RetargetProjectFactory { get; set; }

        public TEmbedded CreateExport()
        {
            return this.RetargetProjectFactory.CreateExport().Value;
        }
    }
}
