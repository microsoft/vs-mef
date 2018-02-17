// Copyright (c) Microsoft. All rights reserved.

#if NET45 || NETSTANDARD2_0

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ExportLifetimeContext<>))]

#elif NETSTANDARD1_0

namespace System.ComponentModel.Composition
{
    /// <summary>
    /// Holds an exported value created by an <see cref="ExportFactory{T}"/>
    /// object and a reference to a method to release that object.
    /// </summary>
    /// <typeparam name="T">The type of the exported value.</typeparam>
    public sealed class ExportLifetimeContext<T> : IDisposable
    {
        /// <summary>
        /// A reference to a method to release the object.
        /// </summary>
        private readonly Action disposeAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportLifetimeContext{T}"/> class.
        /// </summary>
        /// <param name="value">The exported value.</param>
        /// <param name="disposeAction">A reference to a method to release the object.</param>
        public ExportLifetimeContext(T value, Action disposeAction)
        {
            this.Value = value;
            this.disposeAction = disposeAction;
        }

        /// <summary>
        /// Gets the exported value of a <see cref="ExportFactory{T}"/> object.
        /// </summary>
        /// <value>The exported value.</value>
        public T Value { get; }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="ExportLifetimeContext{T}"/>
        /// class, including its associated export.
        /// </summary>
        public void Dispose()
        {
            this.disposeAction?.Invoke();
        }
    }
}

#endif
