namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class LazyPart<T> : Lazy<T>, ILazy<T>
    {
        public LazyPart(Func<T> valueFactory)
            : base(valueFactory, true)
        {
        }

        public LazyPart(Func<object> valueFactory)
            : this(() => (T)valueFactory())
        {
        }

        public Func<T> ValueFactory
        {
            get { return () => this.Value; }
        }
    }
}
