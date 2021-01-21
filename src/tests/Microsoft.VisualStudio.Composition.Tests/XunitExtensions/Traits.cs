// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using Xunit;

    /// <summary>
    /// Well known trait names.
    /// </summary>
    internal static class Traits
    {
        /// <summary>
        /// The key to pass as the first argument for a <see cref="TraitAttribute"/>
        /// so that the test is skipped on mono.
        /// </summary>
        internal const string SkipOnMono = "SkipOnMono";

        /// <summary>
        /// The key to pass as the first argument for a <see cref="TraitAttribute"/>
        /// so that the test is skipped on CoreCLR.
        /// </summary>
        internal const string SkipOnCoreCLR = "SkipOnCoreCLR";
    }
}
