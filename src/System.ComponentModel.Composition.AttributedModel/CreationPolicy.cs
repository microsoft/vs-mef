// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(CreationPolicy))]

#else

namespace System.ComponentModel.Composition
{
    /// <summary>
    /// Specifies when and how a part will be instantiated.
    /// </summary>
    public enum CreationPolicy
    {
        /// <summary>
        /// Specifies that the System.ComponentModel.Composition.Hosting.CompositionContainer
        /// will use the most appropriate System.ComponentModel.Composition.CreationPolicy
        /// for the part given the current context. This is the default System.ComponentModel.Composition.CreationPolicy.
        /// By default, System.ComponentModel.Composition.Hosting.CompositionContainer will
        /// use System.ComponentModel.Composition.CreationPolicy.Shared, unless the System.ComponentModel.Composition.Primitives.ComposablePart
        /// or importer requests System.ComponentModel.Composition.CreationPolicy.NonShared.
        /// </summary>
        Any = 0,

        /// <summary>
        /// Specifies that a single shared instance of the associated System.ComponentModel.Composition.Primitives.ComposablePart
        /// will be created by the System.ComponentModel.Composition.Hosting.CompositionContainer
        /// and shared by all requestors.
        /// </summary>
        Shared = 1,

        /// <summary>
        /// Specifies that a new non-shared instance of the associated System.ComponentModel.Composition.Primitives.ComposablePart
        /// will be created by the System.ComponentModel.Composition.Hosting.CompositionContainer
        /// for every requestor.
        /// </summary>
        NonShared = 2
    }
}

#endif
