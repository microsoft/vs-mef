// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class AssertFailedException : Exception
    {
        internal AssertFailedException()
        {
        }

        internal AssertFailedException(string message)
            : base(message)
        {
        }

        internal AssertFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected AssertFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}