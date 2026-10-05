// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Samples.MetadataViews;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.Composition;
using MefV1 = System.ComponentModel.Composition;

#region InterfaceMetadataView

[MetadataView]
public partial interface IHandlerMetadata
{
    string Name { get; }

    [DefaultValue(false)]
    bool SupportsAsync { get; }
}

[MefV1.Export("Handler", typeof(object))]
[MefV1.ExportMetadata(nameof(IHandlerMetadata.Name), "Text")]
internal class TextHandler
{
}

[MefV1.Export]
internal class InterfaceMetadataImporter
{
    [MefV1.ImportMany("Handler")]
    public IEnumerable<Lazy<object, IHandlerMetadata>> Handlers { get; set; } = null!;
}

#endregion

#region SourceGeneratedMetadataView

[MetadataView]
public partial interface IGeneratedHandlerMetadata
{
    string Name { get; }

    [DefaultValue(false)]
    bool SupportsAsync { get; }

    [DefaultValue(null)]
    Type HandlerType { get; }
}

[MefV1.Export]
internal class SourceGeneratedMetadataImporter
{
    [MefV1.ImportMany("Handler")]
    public IEnumerable<Lazy<object, IGeneratedHandlerMetadata>> Handlers { get; set; } = null!;
}

#endregion

#region LegacyMetadataViewImplementation

[MefV1.MetadataViewImplementation(typeof(LegacyHandlerMetadataView))]
public interface ILegacyHandlerMetadata
{
    string Name { get; }

    [DefaultValue(false)]
    bool SupportsAsync { get; }
}

public class LegacyHandlerMetadataView : ILegacyHandlerMetadata
{
    public LegacyHandlerMetadataView(IDictionary<string, object> metadata)
    {
        this.Name = metadata.ContainsKey(nameof(ILegacyHandlerMetadata.Name))
            ? (string)metadata[nameof(ILegacyHandlerMetadata.Name)]
            : string.Empty;
        this.SupportsAsync = metadata.ContainsKey(nameof(ILegacyHandlerMetadata.SupportsAsync))
            && (bool)metadata[nameof(ILegacyHandlerMetadata.SupportsAsync)];
    }

    public string Name { get; }

    public bool SupportsAsync { get; }
}

#endregion

#region MetadataViewBaseImplementation

[MefV1.MetadataViewImplementation(typeof(HandlerMetadataView))]
public interface IConcreteHandlerMetadata
{
    string Name { get; }

    [DefaultValue(false)]
    bool SupportsAsync { get; }

    [DefaultValue(null)]
    Type HandlerType { get; }
}

public class HandlerMetadataView : MetadataView, IConcreteHandlerMetadata
{
    public string Name => this.GetMetadata<string>();

    public bool SupportsAsync => this.GetMetadata<bool>();

    public Type HandlerType => this.GetMetadata<Type>();
}

#endregion

#region DirectMetadataView

public class DirectHandlerMetadataBase : MetadataView
{
    [DefaultValue(false)]
    public bool SupportsAsync => this.GetMetadata<bool>();
}

public class DirectHandlerMetadata : DirectHandlerMetadataBase
{
    public string Name => this.GetMetadata<string>();

    [DefaultValue(null)]
    public Type HandlerType => this.GetMetadata<Type>();
}

[MefV1.Export]
internal class DirectMetadataImporter
{
    [MefV1.ImportMany("Handler")]
    public IEnumerable<Lazy<object, DirectHandlerMetadata>> Handlers { get; set; } = null!;
}

#endregion
