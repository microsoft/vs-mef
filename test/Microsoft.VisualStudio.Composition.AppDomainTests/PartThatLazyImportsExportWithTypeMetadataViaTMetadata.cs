// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable VSMEF015 // Metadata view interface should be source-generated

namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.ComponentModel;
    using System.Composition;

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
