// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45 || NETSTANDARD2_0

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(CompositionException))]

#else

namespace System.ComponentModel.Composition
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Represents the exception that is thrown when one or more errors occur during
    /// composition in a System.ComponentModel.Composition.Hosting.CompositionContainer
    /// object.
    /// </summary>
    [DebuggerDisplay("{Message}")]
    public class CompositionException : Exception
    {
    }
}

#endif
