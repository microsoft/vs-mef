// Copyright (c) Microsoft. All rights reserved.

#if NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ExportFactory<>))]

#elif NETSTANDARD1_0

namespace System.ComponentModel.Composition
{
    public class ExportFactory<T>
    {
        private Func<Tuple<T, Action>> exportLifetimeContextCreator;

        public ExportFactory(Func<Tuple<T, Action>> exportLifetimeContextCreator)
        {
            if (exportLifetimeContextCreator == null)
            {
                throw new ArgumentNullException("exportLifetimeContextCreator");
            }

            this.exportLifetimeContextCreator = exportLifetimeContextCreator;
        }

        public ExportLifetimeContext<T> CreateExport()
        {
            Tuple<T, Action> untypedLifetimeContext = this.exportLifetimeContextCreator.Invoke();
            return new ExportLifetimeContext<T>(untypedLifetimeContext.Item1, untypedLifetimeContext.Item2);
        }
    }
}

#endif
