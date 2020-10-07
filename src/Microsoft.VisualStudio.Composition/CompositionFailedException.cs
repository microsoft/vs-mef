// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;

    public class CompositionFailedException : Exception
    {
        public CompositionFailedException()
        {
        }

        public CompositionFailedException(string? message)
            : base(message)
        {
        }

        public CompositionFailedException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }

        public CompositionFailedException(string? message, IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>> errors)
            : this(message)
        {
            Requires.NotNull(errors, nameof(errors));
            this.Errors = errors;
        }

        public IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>>? Errors { get; private set; }
    }
}
