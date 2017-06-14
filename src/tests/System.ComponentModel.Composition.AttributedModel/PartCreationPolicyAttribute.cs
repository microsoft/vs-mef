// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(PartCreationPolicyAttribute))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    /// <summary>
    /// Specifies the System.ComponentModel.Composition.PartCreationPolicyAttribute.CreationPolicy
    /// for a part.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PartCreationPolicyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartCreationPolicyAttribute"/> class
        /// with the specified creation policy.
        /// </summary>
        /// <param name="creationPolicy">The creation policy to use.</param>
        public PartCreationPolicyAttribute(CreationPolicy creationPolicy)
        {
            this.CreationPolicy = creationPolicy;
        }

        /// <summary>
        /// Gets a value that indicates the creation policy of the attributed part.
        /// </summary>
        public CreationPolicy CreationPolicy { get; }
    }
}

#endif
