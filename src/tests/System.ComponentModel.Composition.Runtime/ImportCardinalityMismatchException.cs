// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ImportCardinalityMismatchException))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    public class ImportCardinalityMismatchException : Exception
    {
    }
}

#endif
