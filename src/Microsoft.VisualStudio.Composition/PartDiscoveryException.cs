// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Runtime.Serialization;
    using MessagePack;
    using MessagePack.Formatters;

    /// <summary>
    /// An exception that may be thrown during MEF part discovery.
    /// </summary>
    [Serializable]
    [MessagePackFormatter(typeof(Formatter))]
    public class PartDiscoveryException : Exception
    {
        internal string? StackTraceInternal = string.Empty;

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
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable RS0016 // Add public types and members to the declared API
        public override string StackTrace => base.StackTrace ?? this.StackTraceInternal;
#pragma warning restore RS0016 // Add public types and members to the declared API
#pragma warning restore CS8603 // Possible null reference return.

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(this.AssemblyPath), this.AssemblyPath);
            info.AddValue(nameof(this.ScannedType), this.ScannedType);
        }

        private class Formatter : IMessagePackFormatter<PartDiscoveryException?>
        {
            public static readonly Formatter Instance = new();

            private Formatter()
            {
            }

            public PartDiscoveryException? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (reader.TryReadNil())
                {
                    return null;
                }

                options.Security.DepthStep(ref reader);
                try
                {
                    int actualCount = reader.ReadArrayHeader();
                    if (actualCount != 5)
                    {
                        throw new MessagePackSerializationException($"Invalid array count for type {nameof(PartCreationPolicyConstraint)}. Expected: {5}, Actual: {actualCount}");
                    }

                    string message = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                    string? assemblyPath = options.Resolver.GetFormatterWithVerify<string?>().Deserialize(ref reader, options);
                    Type? scannedType = options.Resolver.GetFormatterWithVerify<Type?>().Deserialize(ref reader, options);
                    string? stackTrace = options.Resolver.GetFormatterWithVerify<string?>().Deserialize(ref reader, options);

                    if (!reader.TryReadNil())
                    {
                        Exception innerExceptionObj = Deserialize(ref reader);

                        return new PartDiscoveryException(message, innerExceptionObj)
                        {
                            AssemblyPath = assemblyPath,
                            ScannedType = scannedType,
                            StackTraceInternal = stackTrace,
                        };
                    }
                    else
                    {
                        return new PartDiscoveryException(message)
                        {
                            AssemblyPath = assemblyPath,
                            ScannedType = scannedType,
                            StackTraceInternal = stackTrace,
                        };
                    }
                }
                finally
                {
                    reader.Depth--;
                }

                Exception Deserialize(ref MessagePackReader reader)
                {
                    int actualCount = reader.ReadArrayHeader();
                    if (actualCount != 2)
                    {
                        throw new MessagePackSerializationException($"Invalid array count for type {nameof(PartCreationPolicyConstraint)}. Expected: {2}, Actual: {actualCount}");
                    }

                    var message = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

                    Exception innerException;

                    if (!reader.TryReadNil())
                    {
                        var innerExceptionObj = Deserialize(ref reader);
                        innerException = new Exception(message, innerExceptionObj);
                    }
                    else
                    {
                        innerException = new Exception(message);
                    }

                    return innerException;
                }
            }

            public void Serialize(ref MessagePackWriter writer, PartDiscoveryException? value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(5);

                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Message, options);
                options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.AssemblyPath, options);
                options.Resolver.GetFormatterWithVerify<Type?>().Serialize(ref writer, value.ScannedType, options);
                options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.StackTrace, options);

                if (value.InnerException is not null)
                {
                    int depthLevel = 0;
                    Serialize(ref writer, ref depthLevel, value.InnerException);
                }
                else
                {
                    writer.WriteNil();
                }

                void Serialize(ref MessagePackWriter writer, ref int depthLevel, Exception innerException)
                {
                    writer.WriteArrayHeader(2);

                    options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, innerException.Message, options);

                    if (innerException.InnerException is not null && depthLevel < 2)
                    {
                        depthLevel++; // Avoid infinite recursion.

                        Serialize(ref writer, ref depthLevel, innerException.InnerException);
                    }
                    else
                    {
                        writer.WriteNil();
                    }
                }
            }
        }
    }
}
