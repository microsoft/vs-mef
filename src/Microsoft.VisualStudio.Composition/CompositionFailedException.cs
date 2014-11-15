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

        public CompositionFailedException(string message, IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>> errors)
            : this(message)
        {
            Requires.NotNull(errors, "errors");
            this.Errors = errors;
        }

        public IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>> Errors { get; private set; }
    }
}
