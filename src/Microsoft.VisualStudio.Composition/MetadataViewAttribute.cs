// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System;
using System.Diagnostics;

/// <summary>
/// Annotates an interface as a metadata view, which is a strongly-typed view over the metadata of an export.
/// </summary>
/// <remarks>
/// This attribute serves as a marker for the source generator to identify which interfaces are metadata views and to generate the appropriate implementation classes for them.
/// Because this attribute is marked with <c>Conditional("NEVER")</c>, applications of it are omitted from compiled metadata and therefore do not affect runtime metadata-view resolution.
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
[Conditional("NEVER")]
public sealed class MetadataViewAttribute : Attribute
{
}
