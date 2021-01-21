// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// An exception that may be thrown during MEF part discovery.
    /// </summary>
    [Serializable]
    public class PartDiscoveryException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartDiscoveryException"/> class.
        /// </summary>
        public PartDiscoveryException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartDiscoveryException"/> class.
        /// </summary>
        /// <param name="message"><inheritdoc cref="Exception(string?)" path="/param[@name='message']"/></param>
        public PartDiscoveryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartDiscoveryException"/> class.
        /// </summary>
        /// <param name="message"><inheritdoc cref="Exception(string?, Exception?)" path="/param[@name='message']"/></param>
        /// <param name="innerException"><inheritdoc cref="Exception(string?, Exception?)" path="/param[@name='innerException']"/></param>
        public PartDiscoveryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartDiscoveryException"/> class.
        /// </summary>
        /// <param name="info"><inheritdoc cref="Exception(SerializationInfo, StreamingContext)" path="/param[@name='info']"/></param>
        /// <param name="context"><inheritdoc cref="Exception(SerializationInfo, StreamingContext)" path="/param[@name='context']"/></param>
        protected PartDiscoveryException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.AssemblyPath = info.GetString(nameof(this.AssemblyPath));
            this.ScannedType = (Type?)info.GetValue(nameof(this.ScannedType), typeof(Type));
        }

        /// <summary>
        /// Gets or sets the path to the assembly involved in the failure.
        /// </summary>
        public string? AssemblyPath { get; set; }

        /// <summary>
        /// Gets or sets the type where .NET Reflection failed.
        /// </summary>
        public Type? ScannedType { get; set; }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(this.AssemblyPath), this.AssemblyPath);
            info.AddValue(nameof(this.ScannedType), this.ScannedType);
        }
    }
}
