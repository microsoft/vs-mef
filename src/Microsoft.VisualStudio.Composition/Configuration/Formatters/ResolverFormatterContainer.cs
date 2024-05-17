// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;
    using MessagePack.Resolvers;

    public class ResolverFormatter : IMessagePackFormatter<Resolver>
    {
        /// <inheritdoc/>
        public Resolver Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return options.CompositionResolver();
           // return ResolverFormatterContainer.Resolver;
        }

        public void Serialize(ref MessagePackWriter writer, Resolver value, MessagePackSerializerOptions options)
        {
            //ResolverFormatterContainer.Resolver ??= value;
        }
    }

    internal class ResolverFormatterContainer
    {
        public static Resolver Resolver { get; set; } // set this with MessagePackFormatterContext contructoer
    }

    public static class MessagePackSerializerOptionsExt
    {
        public static bool TryPrepareDeserializeReusableObject<T>(this MessagePackSerializerOptions option, out uint id, out T? value, ref MessagePackReader reader, MessagePackSerializerOptions options) where T : class
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            return messagePackFormatterContext.TryPrepareDeserializeReusableObject(out id, out value, ref reader, options);
        }

        public static void OnDeserializedReusableObject(this MessagePackSerializerOptions option, uint id, object value)
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            messagePackFormatterContext.OnDeserializedReusableObject(id, value);
        }

        public static bool TryPrepareSerializeReusableObject(this MessagePackSerializerOptions option, object value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            return messagePackFormatterContext.TryPrepareSerializeReusableObject(value, ref writer, options);
        }

        public static Resolver CompositionResolver(this MessagePackSerializerOptions option)
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            return messagePackFormatterContext.CompositionResolver;
        }
    }

    internal class MessagePackFormatterContext : MessagePackSerializerOptions, IDisposable
    {
        public Resolver CompositionResolver { get; }

        static IFormatterResolver GetIFormatterResolver(IFormatterResolver resolver)
        {
            return CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                    new IgnoreFormatter<System.Threading.CancellationToken>(),
                    new IgnoreFormatter<System.Threading.Tasks.Task>(),
                    //new IgnoreFormatter<System.Threading.Tasks.Task>(),
                },
                new[] { resolver });
        }

        /// Read
        public MessagePackFormatterContext(IFormatterResolver resolver, Resolver compositionResolver)
            : base(GetIFormatterResolver(resolver))
        {
            //deserializingObjectTable2 = new Dictionary<uint, object?>(); //c heck manual ax count  1000000
            deserializingObjectTable2 = new ConcurrentDictionary<uint, object?>(); //c heck manual ax count  1000000
            this.CompositionResolver = compositionResolver;
        }        

        //writer
        public MessagePackFormatterContext(int estimatedObjectCount, IFormatterResolver resolver, Resolver compositionResolver)
            : base(GetIFormatterResolver(resolver))
        {
            //  serializingObjectTable = new Dictionary<object, uint>(estimatedObjectCount, SmartInterningEqualityComparer.Default);
            serializingObjectTable = new ConcurrentDictionary<object, uint>( SmartInterningEqualityComparer.Default);
            this.CompositionResolver = compositionResolver;

        }

        //static Dictionary<object, uint>? serializingObjectTable;
        //static Dictionary<uint, object?>? deserializingObjectTable2;


        ConcurrentDictionary<object, uint>? serializingObjectTable;
        ConcurrentDictionary<uint, object?>? deserializingObjectTable2;


        public bool TryPrepareDeserializeReusableObject<T>(out uint id, out T? value, ref MessagePackReader reader, MessagePackSerializerOptions options) where T : class
        {
            // deserializingObjectTable2
            // If the id is 0 then return false and value is null
            // If the id is not 0
            //  and the id is in the deserializingObjectTable2 then return false and value is the object
            // and the id is not in the deserializingObjectTable2 then return true and value is null, ask to update the table 
            id = options.Resolver.GetFormatterWithVerify<uint>().Deserialize(ref reader, options);

            if (id == 0)
            {
                value = null;
                return false;
            }

            if (deserializingObjectTable2.TryGetValue(id, out object? obj))
            {
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
            deserializingObjectTable2.TryAdd(id, value);
            //deserializingObjectTable2[id] = value;
        }

        public bool TryPrepareSerializeReusableObject([NotNullWhen(true)] object? value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
        {
            // Searlize
            // If the value is Null then log id ==0, and dont serialize the value
            // if the value if not null
            // if the value in the serializingObjectTable then log the related Id and dont serialize the value
            // if the value is not in the serializingObjectTable then log the new Id and ask to serialize the value
            // Return true when we ask to serialize the value

            if (value is null)
            {
                options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref writer, 0, options);
                return false;
            }

            if (serializingObjectTable.TryGetValue(value, out uint id))
            {
                options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref writer, id, options);

                // The object has already been serialized.
                return false;
            }
            else
            {
                // asking to serialize the object to caller
                serializingObjectTable.TryAdd(value, id = (uint)serializingObjectTable.Count + 1);
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
