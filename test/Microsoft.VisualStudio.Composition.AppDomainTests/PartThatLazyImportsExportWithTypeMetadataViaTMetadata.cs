// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Export]
    public class PartThatLazyImportsExportWithTypeMetadataViaTMetadata
    {
        [Import("AnExportWithMetadataTypeValue")]
        public Lazy<object, IMetadataView> ImportWithTMetadata { get; set; } = null!;
    }

    public interface IMetadataView
    {
        Type SomeType { get; }

        Type[] SomeTypes { get; }

        [DefaultValue("default")]
        string SomeProperty { get; }
    }
}
