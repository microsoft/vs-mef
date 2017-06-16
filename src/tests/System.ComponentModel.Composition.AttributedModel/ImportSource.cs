// Copyright (c) Microsoft. All rights reserved.

#if NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ImportSource))]

#elif NETSTANDARD1_0

namespace System.ComponentModel.Composition
{
    /// <summary>
    /// Option placed on an import to determine how composition searches for exports.
    /// </summary>
    public enum ImportSource
    {
        /// <summary>
        /// The import can be satisfied with values from the current or parent (or other ancestor) containers  (scopes)
        /// </summary>
        Any = 0,

        /// <summary>
        /// The import can be satisfied with values from the current container (scope)
        /// </summary>
        Local = 1,

        /// <summary>
        /// The import can only be satisfied with values from the parent container (or other ancestor containers) (scopes)
        /// </summary>
        NonLocal = 2,
    }
}

#endif
