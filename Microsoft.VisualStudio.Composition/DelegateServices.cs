namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    /// <summary>
    /// Static factory methods for creating .NET Func{T} instances with fewer allocations in some scenarios.
    /// </summary>
    /// <remarks>
    /// These methods employ a neat trick where we take advantage of the fact that Delegate has a field to store
    /// the instance on which to invoke the method. In general, that field is really just the first
    /// argument to pass to the method. So if the method is static, we can use that field to store
    /// something else as the first parameter.
    /// So provided the valueFactory that the caller gave us is a reusable delegate to a static method
    /// that takes one parameter that is a reference type, it means many Func{T} instances can be
    /// constructed for different parameterized values while only incurring the cost of the Func{T} delegate itself
    /// and no closure.
    /// In most cases this is an insignificant difference. But if you're counting allocations for GC pressure,
    /// this might be just what you need. 
    /// </remarks>
    internal static class DelegateServices
    {
        /// <summary>
        /// Creates a Func{T} from a delegate that takes one parameter
        /// (for the cost of a delegate, but without incurring the cost of a closure).
        /// </summary>
        /// <param name="value">The value to return from the lazy.</param>
        /// <returns>The lazy instance.</returns>
        internal static Func<T> FromValue<T>(T value)
            where T : class
        {
            return value.AsFunc();
        }

        /// <summary>
        /// Creates a delegate that invokes another delegate and casts the result to a given type.
        /// </summary>
        /// <typeparam name="T">The type to cast the result of <paramref name="valueFactory"/> to.</typeparam>
        /// <param name="valueFactory">The delegate to chain execution to.</param>
        /// <returns>A delegate which, when invoked, will invoke <paramref name="valueFactory"/>.</returns>
        internal static Func<T> As<T>(this Func<object> valueFactory)
        {
            // This is a very specific syntax that leverages the C# compiler
            // to emit very efficient code for constructing a delegate that
            // uses the "Target" property to store the first parameter to
            // the method.
            // It allows us to construct a Func<T> that returns a T
            // without actually allocating a closure -- we only allocate the delegate.
            return new Func<T>(valueFactory.AsHelper<T>);
        }

        private static Func<T> AsFunc<T>(this T value)
            where T : class
        {
            // This is a very specific syntax that leverages the C# compiler
            // to emit very efficient code for constructing a delegate that
            // uses the "Target" property to store the first parameter to
            // the method.
            // It allows us to construct a Func<T> that returns a T
            // without actually allocating a closure -- we only allocate the delegate.
            return new Func<T>(value.Return);
        }

        private static T Return<T>(this T value)
        {
            return value;
        }

        private static T AsHelper<T>(this Func<object> value)
        {
            return (T)value();
        }
    }
}
