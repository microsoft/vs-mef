namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class LazyPart<T, TMetadata> : Lazy<T, TMetadata>, ILazy<T, TMetadata>
    {
        public LazyPart(Func<T> valueFactory, TMetadata metadata)
            : base(valueFactory, metadata, true)
        {
        }

        public LazyPart(Func<object> valueFactory, TMetadata metadata)
            : base(() => (T)valueFactory(), metadata, true)
        {
        }
    }
}
