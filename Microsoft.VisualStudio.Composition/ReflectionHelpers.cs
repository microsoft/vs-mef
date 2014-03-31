namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class ReflectionHelpers
    {
        internal static IEnumerable<PropertyInfo> EnumProperties(this Type type)
        {
            Requires.NotNull(type, "type");

            // We look at each type in the hierarchy for their individual properties.
            // This allows us to find private property setters defined on base classes,
            // which otherwise we are unable to see.
            var types = new List<Type> { type };
            if (type.GetTypeInfo().IsInterface)
            {
                types.AddRange(type.GetTypeInfo().ImplementedInterfaces);
            }
            else
            {
                while (type != null)
                {
                    type = type.GetTypeInfo().BaseType;
                    if (type != null)
                    {
                        types.Add(type);
                    }
                }
            }

            return types.SelectMany(t => t.GetTypeInfo().DeclaredProperties);
        }

        internal static IEnumerable<PropertyInfo> WherePublicInstance(this IEnumerable<PropertyInfo> infos)
        {
            return infos.Where(p => p.GetMethod.IsPublicInstance() || p.SetMethod.IsPublicInstance());
        }

        internal static IEnumerable<FieldInfo> EnumFields(this Type type)
        {
            Requires.NotNull(type, "type");

            // We look at each type in the hierarchy for their individual properties.
            // This allows us to find private property setters defined on base classes,
            // which otherwise we are unable to see.
            var types = new List<Type> { type };
            if (type.GetTypeInfo().IsInterface)
            {
                types.AddRange(type.GetTypeInfo().ImplementedInterfaces);
            }
            else
            {
                while (type != null)
                {
                    type = type.GetTypeInfo().BaseType;
                    if (type != null)
                    {
                        types.Add(type);
                    }
                }
            }

            return types.SelectMany(t => t.GetTypeInfo().DeclaredFields);
        }

        internal static bool HasParameters(this ConstructorInfo ctor, Type[] parameterTypes)
        {
            var p = ctor.GetParameters();
            if (p.Length != parameterTypes.Length)
            {
                return false;
            }

            for (int i = 0; i < p.Length; i++)
            {
                if (!p[i].ParameterType.Equals(parameterTypes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsStaticExport(this MemberInfo exportingMember)
        {
            if (exportingMember == null)
            {
                return false;
            }

            var exportingField = exportingMember as FieldInfo;
            if (exportingField != null)
            {
                return exportingField.IsStatic;
            }

            var exportingMethod = exportingMember as MethodInfo;
            if (exportingMethod != null)
            {
                return exportingMethod.IsStatic;
            }

            var exportingProperty = exportingMember as PropertyInfo;
            if (exportingProperty != null)
            {
                return exportingProperty.GetMethod.IsStatic;
            }

            throw new NotSupportedException();
        }

        internal static bool IsPublicInstance(this MethodInfo methodInfo)
        {
            return methodInfo.IsPublic && !methodInfo.IsStatic;
        }

        internal static string GetTypeName(Type type, bool genericTypeDefinition, bool evenNonPublic, HashSet<Assembly> relevantAssemblies)
        {
            if (relevantAssemblies != null)
            {
                relevantAssemblies.Add(type.GetTypeInfo().Assembly);
                relevantAssemblies.UnionWith(GetAllBaseTypesAndInterfaces(type).Select(t => t.GetTypeInfo().Assembly));
            }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (!IsPublic(type) && !evenNonPublic)
            {
                return GetTypeName(type.GetTypeInfo().BaseType ?? typeof(object), genericTypeDefinition, evenNonPublic, relevantAssemblies);
            }

            string result = string.Empty;
            if (type.DeclaringType != null)
            {
                result = GetTypeName(type.DeclaringType, genericTypeDefinition, false, relevantAssemblies) + ".";
            }

            if (genericTypeDefinition)
            {
                result += FilterTypeNameForGenericTypeDefinition(type, type.DeclaringType == null);
            }
            else
            {
                string[] typeArguments = type.GetTypeInfo().GenericTypeArguments.Select(t => GetTypeName(t, false, false, relevantAssemblies)).ToArray();
                result += ReplaceBackTickWithTypeArgs(type.DeclaringType == null ? type.FullName : type.Name, typeArguments);
            }

            return result;
        }

        internal static string ReplaceBackTickWithTypeArgs(string originalName, params string[] typeArguments)
        {
            Requires.NotNullOrEmpty(originalName, "originalName");

            string name = originalName;
            int backTickIndex = originalName.IndexOf('`');
            if (backTickIndex >= 0)
            {
                name = originalName.Substring(0, name.IndexOf('`'));
                name += "<";
                int typeArgIndex = originalName.IndexOf('[', backTickIndex + 1);
                string typeArgumentsCountString = originalName.Substring(backTickIndex + 1);
                if (typeArgIndex >= 0)
                {
                    typeArgumentsCountString = typeArgumentsCountString.Substring(0, typeArgIndex - backTickIndex - 1);
                }

                int typeArgumentsCount = int.Parse(typeArgumentsCountString, CultureInfo.InvariantCulture);
                if (typeArguments == null || typeArguments.Length == 0)
                {
                    if (typeArgumentsCount == 1)
                    {
                        name += "T";
                    }
                    else
                    {
                        for (int i = 1; i <= typeArgumentsCount; i++)
                        {
                            name += "T" + i;
                            if (i < typeArgumentsCount)
                            {
                                name += ",";
                            }
                        }
                    }
                }
                else
                {
                    Requires.Argument(typeArguments.Length == typeArgumentsCount, "typeArguments", "Wrong length.");
                    name += string.Join(",", typeArguments);
                }

                name += ">";
            }

            return name;
        }

        internal static bool IsPublic(Type type)
        {
            Requires.NotNull(type, "type");

            if (type.GetTypeInfo().IsNotPublic)
            {
                return false;
            }

            if (type.GetTypeInfo().IsPublic || type.GetTypeInfo().IsNestedPublic)
            {
                return true;
            }

            return false;
        }

        private static string FilterTypeNameForGenericTypeDefinition(Type type, bool fullName)
        {
            Requires.NotNull(type, "type");

            string name = fullName ? type.FullName : type.Name;
            if (type.GetTypeInfo().IsGenericType)
            {
                name = name.Substring(0, name.IndexOf('`'));
                name += "<";
                name += new String(',', type.GetTypeInfo().GenericTypeParameters.Length - 1);
                name += ">";
            }

            return name;
        }

        private static IEnumerable<Type> GetAllBaseTypesAndInterfaces(Type type)
        {
            Requires.NotNull(type, "type");

            for (Type baseType = type.GetTypeInfo().BaseType; baseType != null; baseType = baseType.GetTypeInfo().BaseType)
            {
                yield return baseType;
            }

            foreach (var iface in type.GetTypeInfo().ImplementedInterfaces)
            {
                yield return iface;
            }
        }
    }
}
