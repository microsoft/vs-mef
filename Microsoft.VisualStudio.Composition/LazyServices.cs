namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Validation;

    /// <summary>
    /// Static factory methods for creating .NET Lazy{T} instances with fewer allocations in some scenarios.
    /// </summary>
    /// <remarks>
    /// These methods employ a neat trick where we take advantage of the fact that Delegate has a field to store
    /// the instance on which to invoke the method. In general, that field is really just the first
    /// argument to pass to the method. So if the method is static, we can use that field to store
    /// something else as the first parameter.
    /// So provided the valueFactory that the caller gave us is a reusable delegate to a static method
    /// that takes one parameter that is a reference type, it means many Lazy{T} instances can be
    /// constructed for different parameterized values while only incurring the cost of the Lazy{T} itself
    /// and one delegate (no closure!).
    /// In most cases this is an insignificant difference. But if you're counting allocations for GC pressure,
    /// this might be just what you need. 
    /// </remarks>
    internal static class LazyServices
    {
        private static readonly Dictionary<Type, MethodInfo> closedReturnTValues = new Dictionary<Type, MethodInfo>();

        private static readonly MethodInfo returnObjectValue = typeof(LazyServices).GetMethod("ReturnObjectValue", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo returnTValue = typeof(LazyServices).GetMethod("ReturnTValue", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo createStronglyTypedLazyOfTM = typeof(LazyServices).GetMethod("CreateStronglyTypedLazyOfTM", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo createStronglyTypedLazyOfT = typeof(LazyServices).GetMethod("CreateStronglyTypedLazyOfT", BindingFlags.NonPublic | BindingFlags.Static);

        internal static readonly Type DefaultMetadataViewType = typeof(IDictionary<string, object>);
        internal static readonly Type DefaultExportedValueType = typeof(object);

        /// <summary>
        /// Gets a value indicating whether a type is a Lazy`1 or Lazy`2 type.
        /// </summary>
        /// <param name="type">The type to be tested.</param>
        /// <returns><c>true</c> if <paramref name="type"/> is some Lazy type.</returns>
        internal static bool IsAnyLazyType(this Type type)
        {
            if (type.IsGenericType)
            {
                Type genericTypeDefinition = type.GetGenericTypeDefinition();
                if (typeof(Lazy<>) == genericTypeDefinition || typeof(Lazy<,>) == genericTypeDefinition)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a factory that takes a Func{object} and object-typed metadata
        /// and returns a strongly-typed Lazy{T, TMetadata} instance.
        /// </summary>
        /// <param name="exportType">The type of values created by the Func{object} value factories.</param>
        /// <param name="metadataViewType">The type of metadata passed to the lazy factory.</param>
        /// <returns>A function that takes a Func{object} value factory and metadata, and produces a Lazy{T, TMetadata} instance.</returns>
        internal static Func<Func<object>, object, object> CreateStronglyTypedLazyFactory(Type exportType, Type metadataViewType)
        {
            MethodInfo genericMethod;
            if (metadataViewType != null)
            {
                genericMethod = createStronglyTypedLazyOfTM.MakeGenericMethod(exportType ?? DefaultExportedValueType, metadataViewType);
            }
            else
            {
                genericMethod = createStronglyTypedLazyOfT.MakeGenericMethod(exportType ?? DefaultExportedValueType);
            }

            return (Func<Func<object>, object, object>)Delegate.CreateDelegate(typeof(Func<Func<object>, object, object>), genericMethod);
        }

        /// <summary>
        /// Initializes a Lazy instance for a value that is already constructed
        /// (for the cost of a delegate, but without incurring the cost of a closure).
        /// </summary>
        /// <param name="value">The value to return from the lazy.</param>
        /// <param name="threadSafetyRequired">A value indicating whether a thread-safe instance is required.</param>
        /// <returns>The lazy instance.</returns>
        internal static Lazy<object> FromValue(object value, bool threadSafetyRequired = true)
        {
            var factoryDelegate = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), value, returnObjectValue);
            return new Lazy<object>(factoryDelegate, threadSafetyRequired ? LazyThreadSafetyMode.PublicationOnly : LazyThreadSafetyMode.None);
        }

        /// <summary>
        /// Initializes a Lazy instance for a value that is already constructed
        /// (for the cost of a delegate, but without incurring the cost of a closure).
        /// </summary>
        /// <param name="value">The value to return from the lazy.</param>
        /// <param name="metadata">The metadata to expose on the Lazy instance.</param>
        /// <param name="threadSafetyRequired">A value indicating whether a thread-safe instance is required.</param>
        /// <returns>The lazy instance.</returns>
        internal static Lazy<object, TMetadata> FromValue<TMetadata>(object value, TMetadata metadata, bool threadSafetyRequired = true)
        {
            var factoryDelegate = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), value, returnObjectValue);
            return new Lazy<object, TMetadata>(factoryDelegate, metadata, threadSafetyRequired ? LazyThreadSafetyMode.PublicationOnly : LazyThreadSafetyMode.None);
        }

        /// <summary>
        /// Initializes a Lazy instance for a value that is already constructed
        /// (for the cost of a delegate, but without incurring the cost of a closure).
        /// </summary>
        /// <param name="value">The value to return from the lazy.</param>
        /// <param name="threadSafetyRequired">A value indicating whether a thread-safe instance is required.</param>
        /// <returns>The lazy instance.</returns>
        internal static Lazy<T> FromValue<T>(T value, bool threadSafetyRequired = true)
        {
            MethodInfo returnTValueClosed = GetFromValueGenericFactoryMethod<T>();
            var factoryDelegate = (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), value, returnTValueClosed);
            return new Lazy<T>(factoryDelegate, threadSafetyRequired ? LazyThreadSafetyMode.PublicationOnly : LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Initializes a Lazy instance for a value that is already constructed
        /// (for the cost of a delegate, but without incurring the cost of a closure).
        /// </summary>
        /// <typeparam name="T">The type of value returned by the Lazy instance.</typeparam>
        /// <typeparam name="TMetadata">The type of metadata offered by the Lazy instance.</typeparam>
        /// <param name="value">The value to return from the lazy.</param>
        /// <param name="metadata">The metadata to be offered by the Lazy instance.</param>
        /// <param name="threadSafetyRequired">A value indicating whether a thread-safe instance is required.</param>
        /// <returns>The lazy instance.</returns>
        internal static Lazy<T, TMetadata> FromValue<T, TMetadata>(T value, TMetadata metadata, bool threadSafetyRequired = true)
        {
            MethodInfo returnTValueClosed = GetFromValueGenericFactoryMethod<T>();
            var factoryDelegate = (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), value, returnTValueClosed);
            return new Lazy<T, TMetadata>(factoryDelegate, metadata, threadSafetyRequired ? LazyThreadSafetyMode.PublicationOnly : LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Initializes a Lazy instance with a value factory that takes one argument
        /// (for the cost of a delegate, but without incurring the cost of a closure).
        /// </summary>
        /// <typeparam name="TArg">The type of argument to be passed to the value factory. If a value type, this will be boxed.</typeparam>
        /// <typeparam name="T">The type of value created by the value factory.</typeparam>
        /// <param name="valueFactory">The value factory.</param>
        /// <param name="arg">The argument to be passed to the value factory.</param>
        /// <param name="threadSafety">The thread safety setting for the constructed Lazy instance.</param>
        /// <returns>The constructed Lazy instance.</returns>
        internal static Lazy<T> FromFactory<TArg, T>(Func<TArg, T> valueFactory, TArg arg, LazyThreadSafetyMode threadSafety = LazyThreadSafetyMode.ExecutionAndPublication)
        {
            var parameterizedFactory = (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), arg, valueFactory.Method);
            return new Lazy<T>(parameterizedFactory, threadSafety);
        }

        /// <summary>
        /// Initializes a Lazy instance with a value factory that takes one argument
        /// (for the cost of a delegate, but without incurring the cost of a closure).
        /// </summary>
        /// <typeparam name="TArg">The type of argument to be passed to the value factory. If a value type, this will be boxed.</typeparam>
        /// <typeparam name="T">The type of value created by the value factory.</typeparam>
        /// <typeparam name="TMetadata">The type of metadata exposed by the Lazy instance.</typeparam>
        /// <param name="valueFactory">The value factory.</param>
        /// <param name="arg">The argument to be passed to the value factory.</param>
        /// <param name="metadata">The metadata to pass to the Lazy instance.</param>
        /// <param name="threadSafety">The thread safety setting for the constructed Lazy instance.</param>
        /// <returns>The constructed Lazy instance.</returns>
        internal static Lazy<T, TMetadata> FromFactory<TArg, T, TMetadata>(Func<TArg, T> valueFactory, TArg arg, TMetadata metadata, LazyThreadSafetyMode threadSafety = LazyThreadSafetyMode.ExecutionAndPublication)
        {
            var parameterizedFactory = (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), arg, valueFactory.Method);
            return new Lazy<T, TMetadata>(parameterizedFactory, metadata, threadSafety);
        }

        private static MethodInfo GetFromValueGenericFactoryMethod<T>()
        {
            MethodInfo returnTValueClosed;
            lock (closedReturnTValues)
            {
                closedReturnTValues.TryGetValue(typeof(T), out returnTValueClosed);
            }

            if (returnTValueClosed == null)
            {
                using (var typeArray = ArrayRental<Type>.Get(1))
                {
                    typeArray.Value[0] = typeof(T);
                    returnTValueClosed = returnTValue.MakeGenericMethod(typeArray.Value);
                }

                lock (closedReturnTValues)
                {
                    closedReturnTValues[typeof(T)] = returnTValueClosed;
                }
            }
            return returnTValueClosed;
        }

        private static Lazy<T> CreateStronglyTypedLazyOfT<T>(Func<object> funcOfObject, object metadata)
        {
            Requires.NotNull(funcOfObject, "funcOfObject");

            return FromFactory(f => (T)f(), funcOfObject);
        }

        private static Lazy<T, TMetadata> CreateStronglyTypedLazyOfTM<T, TMetadata>(Func<object> funcOfObject, object metadata)
        {
            Requires.NotNull(funcOfObject, "funcOfObject");
            Requires.NotNullAllowStructs(metadata, "metadata");

            return FromFactory(f => (T)f(), funcOfObject, (TMetadata)metadata);
        }

        private static object ReturnObjectValue(object value)
        {
            return value;
        }

        private static T ReturnTValue<T>(T value)
        {
            return value;
        }
    }
}
