// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(IPartImportsSatisfiedNotification))]

#else

namespace System.ComponentModel.Composition
{
    /// <summary>
    /// Notifies a part when its imports have been satisfied.
    /// </summary>
    public interface IPartImportsSatisfiedNotification
    {
        /// <summary>
        /// Called when a part's imports have been satisfied and it is safe to use.
        /// </summary>
        void OnImportsSatisfied();
    }
}

#endif
