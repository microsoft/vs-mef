// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    [MessagePackFormatter(typeof(Formatter))]
    public class ParameterRef : IEquatable<ParameterRef>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"{this.DeclaringType.FullName}.{this.Method.DebuggerDisplay}(p-index: {this.ParameterIndex})";

        /// <summary>
        /// A cache behind the <see cref="ParameterInfo"/> property.
        /// </summary>
        private ParameterInfo? cachedParameterInfo;

        public ParameterRef(MethodRef method, int parameterIndex)
        {
            Requires.NotNull(method, nameof(method));
            Requires.Range(parameterIndex >= 0, nameof(parameterIndex));

            this.Method = method;
            this.ParameterIndex = parameterIndex;
        }

        public ParameterRef(ParameterInfo parameterInfo, Resolver resolver)
        {
            Requires.NotNull(parameterInfo, nameof(parameterInfo));
            Requires.NotNull(resolver, nameof(resolver));

            this.Method = new MethodRef((MethodBase)parameterInfo.Member, resolver);
            this.ParameterIndex = parameterInfo.Position;
        }

        public MethodRef Method { get; }

        public ParameterInfo? ParameterInfo => this.cachedParameterInfo ?? (this.cachedParameterInfo = this.Resolve());

        public TypeRef DeclaringType => this.Method.DeclaringType;

        public int MethodMetadataToken => this.Method.MetadataToken;

        /// <summary>
        /// Gets a 0-based index describing which parameter in the method this references.
        /// </summary>
        public int ParameterIndex { get; }

        public AssemblyName AssemblyName => this.DeclaringType.AssemblyName;

        internal Resolver Resolver => this.DeclaringType.Resolver;

        [return: NotNullIfNotNull("parameter")]
        public static ParameterRef? Get(ParameterInfo parameter, Resolver resolver)
        {
            if (parameter != null)
            {
                return new ParameterRef(parameter, resolver);
            }

            return default(ParameterRef);
        }

        public bool Equals(ParameterRef? other)
        {
            if (other is null)
            {
                return false;
            }

            return this.Method.Equals(other.Method)
                && this.ParameterIndex == other.ParameterIndex;
        }

        public override int GetHashCode() => unchecked(this.Method.MetadataToken + this.ParameterIndex);

        public override bool Equals(object? obj) => obj is ParameterRef parameter && this.Equals(parameter);

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            this.DeclaringType.GetInputAssemblies(assemblies);
        }

        private class Formatter : IMessagePackFormatter<ParameterRef?>
        {
            public static readonly Formatter Instance = new();
            private const int ExpectedLength = 2;

            private Formatter()
            {
            }

            /// <inheritdoc/>
            public ParameterRef? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (reader.TryReadNil())
                {
                    return null;
                }

                options.Security.DepthStep(ref reader);
                try
                {
                    var actualCount = reader.ReadArrayHeader();
                    if (actualCount != ExpectedLength)
                    {
                        throw new MessagePackSerializationException($"Invalid array count for type {nameof(ParameterRef)}. Expected: {ExpectedLength}, Actual: {actualCount}");
                    }

                    options.Security.DepthStep(ref reader);

                    MethodRef method = options.Resolver.GetFormatterWithVerify<MethodRef>().Deserialize(ref reader, options);
                    int parameterIndex = reader.ReadInt32();
                    return new ParameterRef(method, parameterIndex);
                }
                finally
                {
                    reader.Depth--;
                }
            }

            /// <inheritdoc/>
            public void Serialize(ref MessagePackWriter writer, ParameterRef? value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteArrayHeader(ExpectedLength);

                options.Resolver.GetFormatterWithVerify<MethodRef>().Serialize(ref writer, value.Method, options);
                writer.Write(value.ParameterIndex);
            }
        }
    }
}
