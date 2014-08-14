namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public static class LazyPart
    {
        /// <summary>
        /// A cache of the input and output of the <see cref="FromLazy"/> method.
        /// </summary>
        /// <remarks>
        /// ETL traces show that <see cref="Type.MakeGenericType"/> and <see cref="Type.GenericTypeArguments"/> is *slow*.
        /// </remarks>
        private static readonly Dictionary<Type, Type> FromLazyCache = new Dictionary<Type, Type>();

        public static Lazy<TBase> ToLazy<T, TBase>(this ILazy<T> lazy) where TBase : T
        {
            Requires.NotNull(lazy, "lazy");

            // LazyPart<T> (which implements ILazy<T>) derives from Lazy<T>, so if
            // T === TBase, a simple cast is sufficient.
            var asCast = lazy as Lazy<TBase>;
            if (asCast != null)
            {
                return asCast;
            }

            // Create a new lazy that wraps this one. Always make it thread-safe,
            // not to protect the inner lazy but the outer one.
            // But if the inner lazy is already evaluated, we'll force-evaluate the outer one
            // to match, in which case we don't need thread safety since we're doing it here on just one thread.
            var newLazy = new Lazy<TBase>(() => (TBase)lazy.Value, isThreadSafe: !lazy.IsValueCreated);
            if (lazy.IsValueCreated)
            {
                var throwaway = newLazy.Value;
            }

            return newLazy;
        }

        public static ILazy<T> Wrap<T>(T value)
        {
            return new LazyPart<T>(() => value);
        }

        internal static bool IsAnyLazyType(this Type type)
        {
            if (type == null || !type.GetTypeInfo().IsGenericType)
            {
                return false;
            }

            var typeDefinition = type.GetGenericTypeDefinition();
            return typeDefinition.Equals(typeof(Lazy<>))
                || typeDefinition.Equals(typeof(Lazy<,>))
                || typeDefinition.Equals(typeof(ILazy<>))
                || typeDefinition.Equals(typeof(ILazy<,>));
        }

        internal static bool IsConcreteLazyType(this Type type)
        {
            if (type == null || !type.GetTypeInfo().IsGenericType)
            {
                return false;
            }

            var typeDefinition = type.GetGenericTypeDefinition();
            return typeDefinition.Equals(typeof(Lazy<>))
                || typeDefinition.Equals(typeof(Lazy<,>));
        }

        internal static Type FromLazy(Type type)
        {
            Requires.NotNull(type, "type");

            Type result;
            lock (FromLazyCache)
            {
                FromLazyCache.TryGetValue(type, out result);
            }

            if (result == null)
            {
                if (type.GetTypeInfo().IsGenericType)
                {
                    Type genericTypeDefinition = type.GetGenericTypeDefinition();
                    if (genericTypeDefinition.Equals(typeof(Lazy<>)) || genericTypeDefinition.Equals(typeof(ILazy<>)))
                    {
                        result = typeof(LazyPart<>).MakeGenericType(type.GenericTypeArguments);
                    }
                    else if (genericTypeDefinition.Equals(typeof(Lazy<,>)) || genericTypeDefinition.Equals(typeof(ILazy<,>)))
                    {
                        result = typeof(LazyPart<,>).MakeGenericType(type.GenericTypeArguments);
                    }
                    else
                    {
                        throw new ArgumentException();
                    }

                    lock (FromLazyCache)
                    {
                        FromLazyCache[type] = result;
                    }
                }
                else
                {
                    throw new ArgumentException();
                }
            }

            return result;
        }
    }
}