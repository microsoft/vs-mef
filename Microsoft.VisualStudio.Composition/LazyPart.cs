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
            if (type == null || !type.IsGenericType)
            {
                return false;
            }

            var typeDefinition = type.GetGenericTypeDefinition();
            return typeDefinition.IsEquivalentTo(typeof(Lazy<>))
                || typeDefinition.IsEquivalentTo(typeof(Lazy<,>))
                || typeDefinition.IsEquivalentTo(typeof(ILazy<>))
                || typeDefinition.IsEquivalentTo(typeof(ILazy<,>));
        }

        internal static bool IsConcreteLazyType(this Type type)
        {
            if (type == null || !type.IsGenericType)
            {
                return false;
            }

            var typeDefinition = type.GetGenericTypeDefinition();
            return typeDefinition.IsEquivalentTo(typeof(Lazy<>))
                || typeDefinition.IsEquivalentTo(typeof(Lazy<,>));
        }

        internal static Type FromLazy(Type type)
        {
            Requires.NotNull(type, "type");

            if (type.GetGenericTypeDefinition().IsEquivalentTo(typeof(Lazy<>)))
            {
                return typeof(LazyPart<>).MakeGenericType(type.GenericTypeArguments);
            }
            else if (type.GetGenericTypeDefinition().IsEquivalentTo(typeof(Lazy<,>)))
            {
                return typeof(LazyPart<,>).MakeGenericType(type.GenericTypeArguments);
            }

            throw new ArgumentException();
        }
    }
}