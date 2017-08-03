// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.NonEmbeddingTypeReceiver
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Shell.Interop;
    using MefV1 = System.ComponentModel.Composition;
    using MefV2 = System.Composition;

    public interface IExportedInterface { }

    public class BaseClassForPartThatExportsIVsProjectReference<TInterface, TClass>
        where TInterface : IVsReference
        where TClass : TInterface
    {
    }

    [MefV1.Export(typeof(IExportedInterface))]
    [MefV2.Export(typeof(IExportedInterface))]
    internal class PartThatExportsIVsProjectReference : BaseClassForPartThatExportsIVsProjectReference<IVsProjectReference, VsProjectReference>, IExportedInterface
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
}
