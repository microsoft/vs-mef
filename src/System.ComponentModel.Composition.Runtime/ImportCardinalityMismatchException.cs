// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45 || NETSTANDARD2_0

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ImportCardinalityMismatchException))]

#else

namespace System.ComponentModel.Composition
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// The exception that is thrown when the cardinality of an import is not compatible
    /// with the cardinality of the matching exports.
    /// </summary>
    [DebuggerDisplay("{Message}")]
    public class ImportCardinalityMismatchException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportCardinalityMismatchException"/> class
        /// with a system-supplied message that describes the error.
        /// </summary>
        public ImportCardinalityMismatchException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportCardinalityMismatchException"/> class
        /// with a specified message that describes the error.
        /// </summary>
        /// <param name="message">
        /// A message that describes the <see cref="ImportCardinalityMismatchException"/>,
        /// or null to set the System.Exception.Message property to its default value.
        /// </param>
        public ImportCardinalityMismatchException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportCardinalityMismatchException"/> class
        /// with a specified error message and a reference to the inner exception that
        /// is the cause of this exception.
        /// </summary>
        /// <param name="message">
        /// The message that describes the exception. The caller of this constructor is required
        /// to ensure that this string has been localized for the current system culture.
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception. If the innerException
        /// parameter is not null, the current exception is raised in a catch block that
        /// handles the inner exception.
        /// </param>
        public ImportCardinalityMismatchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

#endif
