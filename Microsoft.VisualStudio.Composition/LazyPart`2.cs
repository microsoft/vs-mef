namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class LazyPart<T, TMetadata> : Lazy<T, TMetadata>, ILazy<T, TMetadata>, IHasValueAndMetadata
    {
        public LazyPart(Func<T> valueFactory, TMetadata metadata)
            : base(valueFactory, metadata, true)
        {
        }

        public LazyPart(Func<object> valueFactory, TMetadata metadata)
            : base(() => (T)valueFactory(), metadata, true)
        {
        }

        Func<T> ILazy<T>.ValueFactory
        {
            get { return () => this.Value; }
        }

        object IHasValueAndMetadata.Value
        {
            get { return this.Value; }
        }

        IReadOnlyDictionary<string, object> IHasValueAndMetadata.Metadata
        {
            get { return (IReadOnlyDictionary<string, object>)this.Metadata; }
        }
    }
}
