namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class ReflectionHelpers
    {
        internal static IEnumerable<PropertyInfo> EnumProperties(this Type type, BindingFlags flags)
        {
            Requires.NotNull(type, "type");

            // We look at each type in the hierarchy for their individual properties.
            // This allows us to find private property setters defined on base classes,
            // which otherwise we are unable to see.
            var types = new List<Type> { type };
            if (type.IsInterface)
            {
                types.AddRange(type.GetInterfaces());
            }
            else
            {
                while (type != null)
                {
                    type = type.BaseType;
                    if (type != null)
                    {
                        types.Add(type);
                    }
                }
            }

            return types.SelectMany(t => t.GetProperties(flags | BindingFlags.DeclaredOnly));
        }
        internal static IEnumerable<FieldInfo> EnumFields(this Type type, BindingFlags flags)
        {
            Requires.NotNull(type, "type");

            // We look at each type in the hierarchy for their individual properties.
            // This allows us to find private property setters defined on base classes,
            // which otherwise we are unable to see.
            var types = new List<Type> { type };
            if (type.IsInterface)
            {
                types.AddRange(type.GetInterfaces());
            }
            else
            {
                while (type != null)
                {
                    type = type.BaseType;
                    if (type != null)
                    {
                        types.Add(type);
                    }
                }
            }

            return types.SelectMany(t => t.GetFields(flags | BindingFlags.DeclaredOnly));
        }
    }
}
