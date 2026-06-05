// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.ComponentModel;
    using System.Composition;
    using MefV1 = System.ComponentModel.Composition;

    [Export]
    public class PartThatLazyImportsExportWithTypeMetadataViaMetadataViewImplementation
    {
        [Import("AnExportWithMetadataTypeValue")]
        public Lazy<object, IMetadataViewWithImplementation> ImportWithTMetadata { get; set; } = null!;
    }

    [MefV1.MetadataViewImplementation(typeof(MetadataViewWithImplementation))]
    public interface IMetadataViewWithImplementation
    {
        Type SomeType { get; }

        Type[] SomeTypes { get; }

        [DefaultValue("default")]
        string SomeProperty { get; }
    }

    public class MetadataViewWithImplementation : MetadataView, IMetadataViewWithImplementation
    {
        public Type SomeType => this.GetMetadata<Type>();

        public Type[] SomeTypes => this.GetMetadata<Type[]>();

        public string SomeProperty => this.GetMetadata<string>();
    }

    [Export]
    public class PartThatLazyImportsExportWithTypeMetadataViaDirectMetadataView
    {
        [Import("AnExportWithMetadataTypeValue")]
        public Lazy<object, DirectMetadataView> ImportWithTMetadata { get; set; } = null!;
    }

    public class DirectMetadataViewBase : MetadataView
    {
        [DefaultValue("default")]
        public string SomeProperty => this.GetMetadata<string>();
    }

    public class DirectMetadataView : DirectMetadataViewBase
    {
        public Type SomeType => this.GetMetadata<Type>();

        public Type[] SomeTypes => this.GetMetadata<Type[]>();
    }
}
