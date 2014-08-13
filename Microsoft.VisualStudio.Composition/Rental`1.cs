namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal struct Rental<T> : IDisposable
        where T : class
    {
        private T value;
        private Stack<T> returnTo;
        private Action<T> cleanup;

        internal Rental(Stack<T> returnTo, Func<int, T> create, Action<T> cleanup, int createArg)
        {
            this.value = returnTo != null && returnTo.Count > 0 ? returnTo.Pop() : create(createArg);
            this.returnTo = returnTo;
            this.cleanup = cleanup;
        }

        public T Value
        {
            get { return this.value; }
        }

        public void Dispose()
        {
            Assumes.NotNull(this.value);

            var value = this.value;
            this.value = null;
            if (this.cleanup != null)
            {
                this.cleanup(value);
            }

            if (this.returnTo != null)
            {
                this.returnTo.Push(value);
            }
        }
    }
}
