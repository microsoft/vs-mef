namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class LazyServices
    {
        private static readonly MethodInfo createStronglyTypedLazyOfTM = typeof(LazyServices).GetMethod("CreateStronglyTypedLazyOfTM", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo createStronglyTypedLazyOfT = typeof(LazyServices).GetMethod("CreateStronglyTypedLazyOfT", BindingFlags.NonPublic | BindingFlags.Static);

        internal static readonly Type DefaultMetadataViewType = typeof(IDictionary<string, object>);
        internal static readonly Type DefaultExportedValueType = typeof(object);

        internal static bool IsAnyLazyType(this Type type)
        {
            if (type.IsGenericType)
            {
                Type genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Lazy<>) || genericTypeDefinition == typeof(Lazy<,>))
                {
                    return true;
                }
            }

            return false;
        }

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

        internal static Lazy<T> CreateStronglyTypedLazyOfT<T>(Func<object> funcOfObject, object metadata)
        {
            return new Lazy<T>(() => (T)funcOfObject());
        }

        internal static Lazy<T> CreateStronglyTypedLazyOfTM<T, TMetadata>(Func<object> funcOfObject, object metadata)
        {
            return new Lazy<T, TMetadata>(() => (T)funcOfObject(), (TMetadata)metadata);
        }
    }
}
