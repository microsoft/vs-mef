// Copyright (c) Microsoft. All rights reserved.

#if NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ExportFactory<,>))]

#elif NETSTANDARD1_0

namespace System.ComponentModel.Composition
{
    public class ExportFactory<T, TMetadata> : ExportFactory<T>
    {
        public ExportFactory(Func<Tuple<T, Action>> exportLifetimeContextCreator, TMetadata metadata)
            : base(exportLifetimeContextCreator)
        {
            this.Metadata = metadata;
        }

        public TMetadata Metadata { get; }
    }
}

#endif
