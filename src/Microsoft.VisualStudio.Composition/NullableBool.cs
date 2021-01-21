// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;

    /// <summary>
    /// A nullable boolean value with atomic reads and writes.
    /// </summary>
    /// <remarks>
    /// The <see cref="Nullable{T}"/> type has two fields which prevent it being initialized or copied atomically as a single "word".
    /// This type has just one field (which it can do since we specialize in storing <see cref="bool"/> values), so it is just one word and therefore atomic.
    /// </remarks>
    internal struct NullableBool
    {
        /// <summary>
        /// A tri-state backing field for the <see cref="Value"/> property. 0 means not computed, -1 means false and 1 means true.
        /// </summary>
        /// <remarks>
        /// We use a tri-state field to support lock-free atomic writes.
        /// </remarks>
        private int state;

        /// <summary>
        /// Initializes a new instance of the <see cref="NullableBool"/> struct.
        /// </summary>
        /// <param name="value">The initial value of the boolean.</param>
        internal NullableBool(bool value)
        {
            this.state = value ? 1 : -1;
        }

        /// <summary>
        /// Wraps a boolean value in a <see cref="NullableBool"/> struct.
        /// </summary>
        /// <param name="value">The boolean value to wrap.</param>
        public static implicit operator NullableBool(bool value) => new NullableBool(value);

        /// <summary>
        /// Gets a value indicating whether the boolean value has been computed.
        /// </summary>
        internal bool HasValue => this.state != 0;

        /// <summary>
        /// Gets or sets a value indicating whether the boolean value is <c>true</c>.
        /// </summary>
        internal bool Value
        {
            get
            {
                switch (this.state)
                {
                    case 1: return true;
                    case -1: return false;
                    case 0: throw new InvalidOperationException("No value.");
                    default: throw Assumes.NotReachable();
                }
            }

            set
            {
                this.state = value ? 1 : -1;
            }
        }
    }
}
