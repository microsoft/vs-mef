namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface ILazy<out T, out TMetadata> : ILazy<T>
    {
        TMetadata Metadata { get; }
    }
}
