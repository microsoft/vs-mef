// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(PartMetadataAttribute))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    /// <summary>
    /// Specifies metadata for a type to be used as a ComposablePartDefinition and
    /// ComposablePart.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PartMetadataAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PartMetadataAttribute"/> class with the
        ///     specified name and metadata value.
        /// </summary>
        /// <param name="name">
        ///     A <see cref="string"/> containing the name of the metadata value; or
        ///     <see langword="null"/> to use an empty string ("").
        /// </param>
        /// <param name="value">
        ///     An <see cref="object"/> containing the metadata value. This can be
        ///     <see langword="null"/>.
        /// </param>
        public PartMetadataAttribute(string name, object value)
        {
            this.Name = name ?? string.Empty;
            this.Value = value;
        }

        /// <summary>
        /// Gets the name of the metadata value.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the metadata value.
        /// </summary>
        public object Value { get; }
    }
}

#endif
