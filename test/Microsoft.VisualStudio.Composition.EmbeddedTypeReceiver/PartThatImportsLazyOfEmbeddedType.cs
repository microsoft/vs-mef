// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.EmbeddedTypeReceiver
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Shell.Interop;
    using MefV1 = System.ComponentModel.Composition;
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
    [MefV1.Export, MefV2.Export]
    public class PartThatImportsLazyOfEmbeddedType
    {
        [MefV1.Import, MefV2.Import]
        public Lazy<TEmbedded> RetargetProject { get; set; } = null!;

        public TEmbedded RetargetProjectNoLazy
        {
            get { return this.RetargetProject.Value; }
        }
    }

    [MefV1.Export, MefV2.Export]
    public class PartThatImportsLazyOfEmbeddedTypeNonPublic
    {
        public TEmbedded RetargetProjectNoLazy
        {
            get { return this.RetargetProject.Value; }
        }

        [MefV1.Import, MefV2.Import]
        internal Lazy<TEmbedded> RetargetProject { get; set; } = null!;
    }

    [MefV1.Export, MefV2.Export]
    public class PartThatImportsEmbeddedType
    {
        [MefV1.Import, MefV2.Import]
        public TEmbedded RetargetProject { get; set; } = null!;
    }

    /// <summary>
    /// The type must appear in a different assembly from the exporting part
    /// so that the two assemblies have distinct Type instances for the embeddable interface.
    /// </summary>
    [MefV1.Export]
    public class PartThatImportsExportFactoryOfEmbeddedTypeV1
    {
        [MefV1.Import]
        public MefV1.ExportFactory<TEmbedded> RetargetProjectFactory { get; set; } = null!;

        public TEmbedded CreateExport()
        {
            return this.RetargetProjectFactory.CreateExport().Value;
        }
    }

    /// <summary>
    /// The type must appear in a different assembly from the exporting part
    /// so that the two assemblies have distinct Type instances for the embeddable interface.
    /// </summary>
    [MefV2.Export]
    public class PartThatImportsExportFactoryOfEmbeddedTypeV2
    {
        [MefV2.Import]
        public MefV2.ExportFactory<TEmbedded> RetargetProjectFactory { get; set; } = null!;

        public TEmbedded CreateExport()
        {
            return this.RetargetProjectFactory.CreateExport().Value;
        }
    }
}
