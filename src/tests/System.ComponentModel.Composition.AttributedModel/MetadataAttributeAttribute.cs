// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(MetadataAttributeAttribute))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    /// <summary>
    /// Specifies that a custom attribute's properties provide metadata for exports applied
    /// to the same type, property, field, or method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class MetadataAttributeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataAttributeAttribute"/> class.
        /// </summary>
        public MetadataAttributeAttribute()
        {
        }
    }
}

#endif
