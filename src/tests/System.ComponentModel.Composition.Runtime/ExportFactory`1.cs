// Copyright (c) Microsoft. All rights reserved.

#if NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ExportFactory<>))]

#elif NETSTANDARD1_0

namespace System.ComponentModel.Composition
{
    /// <summary>
    /// A factory that creates new instances of a part that provides the specified export.
    /// </summary>
    /// <typeparam name="T">The type of the export.</typeparam>
    public class ExportFactory<T>
    {
        /// <summary>
        /// A function that returns the exported value and an System.Action that releases it.
        /// </summary>
        private readonly Func<Tuple<T, Action>> exportLifetimeContextCreator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportFactory{T}"/> class.
        /// </summary>
        /// <param name="exportLifetimeContextCreator">
        /// A function that returns the exported value and an System.Action that releases it.
        /// </param>
        public ExportFactory(Func<Tuple<T, Action>> exportLifetimeContextCreator)
        {
            this.exportLifetimeContextCreator = exportLifetimeContextCreator ?? throw new ArgumentNullException(nameof(exportLifetimeContextCreator));
        }

        /// <summary>
        /// Creates an instance of the factory's export type.
        /// </summary>
        /// <returns>A valid instance of the factory's exported type.</returns>
        public ExportLifetimeContext<T> CreateExport()
        {
            Tuple<T, Action> untypedLifetimeContext = this.exportLifetimeContextCreator.Invoke();
            return new ExportLifetimeContext<T>(untypedLifetimeContext.Item1, untypedLifetimeContext.Item2);
        }
    }
}

#endif
