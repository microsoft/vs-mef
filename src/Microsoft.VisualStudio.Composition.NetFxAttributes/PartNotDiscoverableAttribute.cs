// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(PartNotDiscoverableAttribute))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    /// <summary>
    /// Place on a type that should not be discovered as a ComposablePart in
    /// a ComposablePartCatalog.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PartNotDiscoverableAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartNotDiscoverableAttribute"/> class.
        /// </summary>
        public PartNotDiscoverableAttribute()
        {
        }
    }
}

#endif
