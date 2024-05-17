// Copyright (c) Microsoft Corporation. All rights reserved. Licensed under the MIT license. See
// LICENSE file in the project root for full license information. Copyright (c) Microsoft
// Corporation. All rights reserved. Licensed under the MIT license. See LICENSE file in the project
// root for full license information. Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information. Copyright (c) Microsoft Corporation. All rights reserved. Licensed under the MIT
// license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;
    using MessagePack.Resolvers;

    internal class MessagePackFormatterContext : MessagePackSerializerOptions, IDisposable
    {
        public Resolver CompositionResolver { get; }

        private static IFormatterResolver GetIFormatterResolver(IFormatterResolver resolver) => CompositeResolver.Create(
                [
                    new IgnoreFormatter<System.Threading.CancellationToken>(),
                    new IgnoreFormatter<System.Threading.Tasks.Task>(),
                ],
                [resolver]);

        public MessagePackFormatterContext(IFormatterResolver resolver, Resolver compositionResolver)
            : base(GetIFormatterResolver(resolver))
        {
            this.deserializingObjectTable = new ConcurrentDictionary<uint, object?>(); //check manual ax count  1000000
            this.CompositionResolver = compositionResolver;
        }

        public MessagePackFormatterContext(int estimatedObjectCount, IFormatterResolver resolver, Resolver compositionResolver)
            : base(GetIFormatterResolver(resolver))
        {
            this.serializingObjectTable = new ConcurrentDictionary<object, uint>(SmartInterningEqualityComparer.Default);
            this.CompositionResolver = compositionResolver;
        }

        private ConcurrentDictionary<object, uint>? serializingObjectTable;
        private ConcurrentDictionary<uint, object?>? deserializingObjectTable;

        public bool TryPrepareDeserializeReusableObject<T>(out uint id, out T? value, ref MessagePackReader reader, MessagePackSerializerOptions options) where T : class
        {
            id = options.Resolver.GetFormatterWithVerify<uint>().Deserialize(ref reader, options);

            if (id == 0)
            {
                value = null;
                return false;
            }

            if (this.deserializingObjectTable.TryGetValue(id, out object? obj))
            {
                // The object has already been deserialized.
                value = (T?)obj;
                return false;
            }
            else
            {
                value = null;

                // asking to deserialize the object to caller
                return true;
            }
        }

        public void OnDeserializedReusableObject(uint id, object value)
        {
            this.deserializingObjectTable.TryAdd(id, value);
        }

        public bool TryPrepareSerializeReusableObject([NotNullWhen(true)] object? value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref writer, 0, options);
                return false;
            }

            if (this.serializingObjectTable.TryGetValue(value, out uint id))
            {
                options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref writer, id, options);

                // The object has already been serialized.
                return false;
            }
            else
            {
                // asking to serialize the object to caller
                this.serializingObjectTable.TryAdd(value, id = (uint)this.serializingObjectTable.Count + 1);
                options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref writer, id, options);
                return true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.serializingObjectTable?.Clear();
                this.deserializingObjectTable?.Clear();
            }
        }

        private class SmartInterningEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly IEqualityComparer<object> Default = new SmartInterningEqualityComparer();

            private static readonly IEqualityComparer<object> Fallback = EqualityComparer<object>.Default;

            private SmartInterningEqualityComparer()
            {
            }

            public new bool Equals(object? x, object? y)
            {
                if (x is AssemblyName && y is AssemblyName)
                {
                    return ByValueEquality.AssemblyName.Equals((AssemblyName)x, (AssemblyName)y);
                }

                return Fallback.Equals(x, y);
            }

            public int GetHashCode(object obj)
            {
                if (obj is AssemblyName)
                {
                    return ByValueEquality.AssemblyName.GetHashCode((AssemblyName)obj);
                }

                return Fallback.GetHashCode(obj);
            }
        }
    }
}
