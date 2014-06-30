namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class CompositionFailedException : Exception
    {
        public CompositionFailedException() { }

        public CompositionFailedException(string message) : base(message) { }

        public CompositionFailedException(string message, Exception innerException) : base(message, innerException) { }

        public CompositionFailedException(string message, IEnumerable<ComposedPartDiagnostic> errors)
            : this(message)
        {
            this.Errors = ImmutableList.CreateRange(errors);
        }

        public ImmutableList<ComposedPartDiagnostic> Errors { get; private set; }
    }
}
