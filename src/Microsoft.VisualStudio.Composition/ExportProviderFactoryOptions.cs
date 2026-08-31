// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System;

/// <summary>
/// Controls optional behavior when creating an export provider factory.
/// </summary>
[Flags]
public enum ExportProviderFactoryOptions
{
    /// <summary>
    /// Uses the default export provider behavior.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enables tiered expression compilation for repeatedly activated non-shared and sharing-boundary parts.
    /// </summary>
    EnableActivationExpressionCompilation = 0x1,
}
