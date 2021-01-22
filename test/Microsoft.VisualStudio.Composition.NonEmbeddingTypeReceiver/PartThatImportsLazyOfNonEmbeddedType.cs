// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1649 // File name should match first type name

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

        public string? FullPath { get; set; }

        public string? Name { get; set; }
    }

    public class VsProjectReference : VsReferenceBase, IVsProjectReference
    {
        public string? Identity { get; set; }

        public string? ReferenceSpecification { get; set; }
    }
}
