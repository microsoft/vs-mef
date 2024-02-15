// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace Microsoft.VisualStudio.Composition;

using System.Diagnostics;
using System.Reflection;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
internal class ComposedLazy<T>(AssemblyName? assemblyName, Func<T> valueFactory) : Lazy<T>(valueFactory), IComposedLazy
{
    public AssemblyName? AssemblyName => assemblyName;

    internal static string GetDebuggerDisplay(Lazy<T> lazy)
    {
        return lazy.IsValueCreated ? $"Activated MEF export ({lazy.Value?.GetType().FullName ?? "null"})" : "Unactivated MEF export";
    }

    private string DebuggerDisplay => GetDebuggerDisplay(this);
}

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
internal class ComposedLazy<T, TMetadata>(AssemblyName? assemblyName, Func<T> valueFactory, TMetadata metadata) : Lazy<T, TMetadata>(valueFactory, metadata), IComposedLazy
{
    public AssemblyName? AssemblyName => assemblyName;

    private string DebuggerDisplay => ComposedLazy<T>.GetDebuggerDisplay(this);
}

/// <summary>
/// An interface that <em>should</em> be implemented by every <see cref="Lazy{T}"/> and <see cref="Lazy{T, TMetadata}"/> export
/// that MEF creates (whether by import or via <see cref="ExportProvider.GetExports{T}()"/> calls.
/// </summary>
internal interface IComposedLazy
{
    /// <summary>
    /// Gets the name of the assembly that exports the value, if known.
    /// </summary>
    AssemblyName? AssemblyName { get; }
}
