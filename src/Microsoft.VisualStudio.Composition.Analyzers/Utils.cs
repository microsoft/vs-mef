// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.CodeAnalysis.Diagnostics;

    /// <summary>
    /// Utility methods for analyzers.
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// Gets the URL to the help topic for a particular analyzer.
        /// </summary>
        /// <param name="analyzerId">The ID of the analyzer.</param>
        /// <returns>The URL for the analyzer's documentation.</returns>
        internal static string GetHelpLink(string analyzerId)
        {
            return $"https://github.com/Microsoft/vs-mef/blob/main/doc/analyzers/{analyzerId}.md";
        }
    }
}
