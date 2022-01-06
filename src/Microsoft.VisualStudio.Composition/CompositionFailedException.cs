// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;

    /// <summary>
    /// An exception thrown when failures occur during composition.
    /// </summary>
    [Serializable]
    public class CompositionFailedException : Exception
    {
        private string? errorsAsString;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionFailedException"/> class.
        /// </summary>
        public CompositionFailedException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionFailedException"/> class.
        /// </summary>
        /// <param name="message"><inheritdoc cref="Exception(string?)" path="/param[@name='message']"/></param>
        public CompositionFailedException(string? message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionFailedException"/> class.
        /// </summary>
        /// <param name="message"><inheritdoc cref="Exception(string?, Exception?)" path="/param[@name='message']"/></param>
        /// <param name="innerException"><inheritdoc cref="Exception(string?, Exception?)" path="/param[@name='innerException']"/></param>
        public CompositionFailedException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionFailedException"/> class.
        /// </summary>
        /// <param name="message"><inheritdoc cref="CompositionFailedException(string?)" path="/param[@name='message']"/></param>
        /// <param name="errors">
        /// The errors that occurred during composition.
        /// <inheritdoc cref="CompositionConfiguration.CompositionErrors" path="/remarks"/>
        /// </param>
        public CompositionFailedException(string? message, IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>> errors)
            : this(message)
        {
            Requires.NotNull(errors, nameof(errors));
            this.Errors = errors;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionFailedException"/> class.
        /// </summary>
        /// <param name="info"><inheritdoc cref="Exception(SerializationInfo, StreamingContext)" path="/param[@name='info']"/></param>
        /// <param name="context"><inheritdoc cref="Exception(SerializationInfo, StreamingContext)" path="/param[@name='context']"/></param>
        protected CompositionFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.errorsAsString = info.GetString(nameof(this.ErrorsAsString));
        }

        /// <summary>
        /// Gets the compositional errors that occurred.
        /// </summary>
        /// <remarks>
        /// This collection is not serialized via the <see cref="ISerializable"/> interface.
        /// Refer to <see cref="ErrorsAsString"/> for a serialized form of these errors.
        /// </remarks>
        public IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>>? Errors { get; private set; }

        /// <summary>
        /// Gets a string representation of the compositional errors that are described in <see cref="Errors"/>
        /// (or were, before serialization dropped that data).
        /// </summary>
        public string? ErrorsAsString
        {
            get
            {
                if (this.errorsAsString is null && this.Errors is object)
                {
                    using var writer = new StringWriter();
                    Utilities.WriteErrors(writer, this.Errors);
                    this.errorsAsString = writer.ToString();
                }

                return this.errorsAsString;
            }
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(this.ErrorsAsString), this.ErrorsAsString);
       }
    }
}
