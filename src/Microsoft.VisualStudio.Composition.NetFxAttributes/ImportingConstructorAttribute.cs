// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ImportingConstructorAttribute))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    /// <summary>
    ///     Specifies that a constructor should be used when constructing an attributed part.
    /// </summary>
    /// <remarks>
    ///     By default, only a default parameter-less constructor, if available, is used to
    ///     construct an attributed part. Use this attribute to indicate that a specific constructor
    ///     should be used.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public class ImportingConstructorAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ImportingConstructorAttribute"/> class.
        /// </summary>
        public ImportingConstructorAttribute()
        {
        }
    }
}

#endif
