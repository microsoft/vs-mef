// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;

    public class ResolverFormatter : IMessagePackFormatter<Resolver>
    {
        /// <inheritdoc/>
        public Resolver Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return ResolverFormatterContainer.Resolver;
        }

        public void Serialize(ref MessagePackWriter writer, Resolver value, MessagePackSerializerOptions options)
        {
            ResolverFormatterContainer.Resolver ??= value;
        }
    }

    internal class ResolverFormatterContainer
    {
        public static Resolver Resolver { get; set; } // set this with MessagePackFormatterContext contructoer
    }

    internal class MessagePackFormatterContext : IDisposable
    {
        /// Read
        public MessagePackFormatterContext()
        {
            //deserializingObjectTable2 = new Dictionary<uint, object?>(); //c heck manual ax count  1000000
            deserializingObjectTable2 = new ConcurrentDictionary<uint, object?>(); //c heck manual ax count  1000000
        }

        //writer
        public MessagePackFormatterContext(int estimatedObjectCount)
        {
            //  serializingObjectTable = new Dictionary<object, uint>(estimatedObjectCount, SmartInterningEqualityComparer.Default);
            serializingObjectTable = new ConcurrentDictionary<object, uint>( SmartInterningEqualityComparer.Default);
        }

        //static Dictionary<object, uint>? serializingObjectTable;
        //static Dictionary<uint, object?>? deserializingObjectTable2;


        static ConcurrentDictionary<object, uint>? serializingObjectTable;
        static ConcurrentDictionary<uint, object?>? deserializingObjectTable2;


        public static bool TryPrepareDeserializeReusableObject<T>(out uint id, out T? value, ref MessagePackReader reader, MessagePackSerializerOptions options) where T : class
        {
            id = options.Resolver.GetFormatterWithVerify<uint>().Deserialize(ref reader, options);
            if (id == 0)
            {
                value = null;
                return false;
            }

            if(deserializingObjectTable2.ContainsKey(id))
            {
                // key is there, re use the sapace
                value = (T?)deserializingObjectTable2[id];
            }
            else
            {
                //key is no there, allocates space
                //deserializingObjectTable2[id] = null;
                deserializingObjectTable2.TryAdd(id, null);

                value = null;
            }

            bool result = value is null;
            return result;
        }

        public static void OnDeserializedReusableObject(uint id, object value)
        {
            deserializingObjectTable2[id] = value;
        }

        public static bool TryPrepareSerializeReusableObject([NotNullWhen(true)] object? value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
        {
            uint id;
            bool result;
            if (value == null)
            {
                id = 0;
                result = false;
            }
            else if (serializingObjectTable.TryGetValue(value, out id))
            {
                // The object has already been serialized.
                result = false;
            }
            else
            {
                serializingObjectTable.TryAdd(value, id = (uint)serializingObjectTable.Count + 1);
                result = true;
            }

#if TRACESERIALIZATION
            if (id != 0)
            {
                this.trace.WriteLine((result ? "Start" : "Reuse") + $" object {id}.");
            }
#endif
            options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref writer, id, options);
            return result;
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
                serializingObjectTable?.Clear();
                deserializingObjectTable2?.Clear();

#if TRACESERIALIZATION || TRACESTATS
                this.trace.Dispose();
#endif
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
