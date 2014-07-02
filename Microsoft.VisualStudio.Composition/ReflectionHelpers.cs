namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public static class ReflectionHelpers
    {
        private static readonly MethodInfo CastAsFuncMethodInfo = new Func<Func<object>, Delegate>(CastAsFunc<object>).GetMethodInfo().GetGenericMethodDefinition();

        public static object CreateFuncOfType(Type typeArg, Func<object> func)
        {
            return CastAsFuncMethodInfo.MakeGenericMethod(typeArg).Invoke(null, new object[] { func });
        }

        internal static bool IsEquivalentTo(this Type type1, Type type2)
        {
            Requires.NotNull(type1, "type1");
            Requires.NotNull(type2, "type2");

            if (type1 == type2)
            {
                return true;
            }

            var type1Info = type1.GetTypeInfo();
            var type2Info = type2.GetTypeInfo();
            return type1Info.IsAssignableFrom(type2Info)
                && type2Info.IsAssignableFrom(type1Info);
        }

        internal static bool IsAssignableTo(ImportDefinitionBinding import, ExportDefinitionBinding export)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(export, "export");

            var receivingType = import.ImportingSiteElementType;
            var exportingType = export.ExportedValueType;
            if (exportingType.GetTypeInfo().IsGenericTypeDefinition && receivingType.GetTypeInfo().IsGenericType)
            {
                exportingType = exportingType.MakeGenericType(receivingType.GenericTypeArguments);
            }

            if (typeof(Delegate).GetTypeInfo().IsAssignableFrom(receivingType.GetTypeInfo()) && typeof(Delegate).GetTypeInfo().IsAssignableFrom(exportingType.GetTypeInfo()))
            {
                // Delegates of varying types may be assigned to each other.
                // For example Action<object, EventArgs> can be assigned to EventHandler.
                // The simplest way to test for it is to ask the CLR to do it.
                // http://stackoverflow.com/questions/23075298/how-to-detect-compatibility-between-delegate-types/23088194#23088194
                try
                {
                    ((MethodInfo)export.ExportingMember).CreateDelegate(receivingType, null);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
            else
            {
                // Utilize the standard assignability checks for everything else.
                return receivingType.GetTypeInfo().IsAssignableFrom(exportingType.GetTypeInfo());
            }
        }

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

        internal static IEnumerable<Type> EnumTypeAndBaseTypes(this Type type)
        {
            Requires.NotNull(type, "type");

            while (type != null)
            {
                yield return type;
                type = type.GetTypeInfo().BaseType;
            }
        }

        internal static IEnumerable<IGrouping<Type, T>> GetCustomAttributesByType<T>(this Type type)
            where T : Attribute
        {
            Requires.NotNull(type, "type");

            return from t in EnumTypeAndBaseTypes(type)
                   from attribute in t.GetTypeInfo().GetCustomAttributes<T>(false)
                   let usage = attribute.GetType().GetTypeInfo().GetCustomAttribute<AttributeUsageAttribute>()
                   where t.Equals(type) || (usage != null && usage.Inherited)
                   group attribute by t into attributesByType
                   select attributesByType;
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

        internal static Type GetMemberType(MemberInfo fieldOrProperty)
        {
            Requires.NotNull(fieldOrProperty, "fieldOrProperty");

            var property = fieldOrProperty as PropertyInfo;
            if (property != null)
            {
                return property.PropertyType;
            }

            var field = fieldOrProperty as FieldInfo;
            if (field != null)
            {
                return field.FieldType;
            }

            throw new ArgumentException("Unexpected member type.");
        }

        internal static bool IsPublicInstance(this MethodInfo methodInfo)
        {
            return methodInfo.IsPublic && !methodInfo.IsStatic;
        }

        internal static string GetTypeName(Type type, bool genericTypeDefinition, bool evenNonPublic, HashSet<Assembly> relevantAssemblies, HashSet<Type> relevantEmbeddedTypes)
        {
            if (relevantAssemblies != null)
            {
                relevantAssemblies.Add(type.GetTypeInfo().Assembly);
                relevantAssemblies.UnionWith(GetAllBaseTypesAndInterfaces(type).Select(t => t.GetTypeInfo().Assembly));
            }

            if (relevantEmbeddedTypes != null)
            {
                AddEmbeddedInterfaces(type, relevantEmbeddedTypes);
            }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (!IsPublic(type, checkGenericTypeArgs: true) && !evenNonPublic)
            {
                return GetTypeName(type.GetTypeInfo().BaseType ?? typeof(object), genericTypeDefinition, evenNonPublic, relevantAssemblies, relevantEmbeddedTypes);
            }

            string result = string.Empty;
            if (type.DeclaringType != null)
            {
                result = GetTypeName(type.DeclaringType, genericTypeDefinition, false, relevantAssemblies, relevantEmbeddedTypes) + ".";
            }

            if (genericTypeDefinition)
            {
                result += FilterTypeNameForGenericTypeDefinition(type, type.DeclaringType == null);
            }
            else
            {
                string[] typeArguments = type.GetTypeInfo().GenericTypeArguments.Select(t => GetTypeName(t, false, evenNonPublic, relevantAssemblies, relevantEmbeddedTypes)).ToArray();
                result += ReplaceBackTickWithTypeArgs(type.DeclaringType == null ? type.FullName : type.Name, typeArguments);
            }

            return result;
        }

        private static void AddEmbeddedInterfaces(Type type, HashSet<Type> relevantEmbeddedTypes)
        {
            if (type.IsEmbeddedType())
            {
                relevantEmbeddedTypes.Add(type);
            }

            foreach (Type iface in type.GetTypeInfo().ImplementedInterfaces)
            {
                AddEmbeddedInterfaces(iface, relevantEmbeddedTypes);
            }
        }

        internal static string ReplaceBackTickWithTypeArgs(string originalName, params string[] typeArguments)
        {
            Requires.NotNullOrEmpty(originalName, "originalName");

            string name = originalName;
            int backTickIndex = originalName.IndexOf('`');
            if (backTickIndex >= 0)
            {
                int typeArgCountLength = originalName.ToCharArray().Skip(backTickIndex + 1).TakeWhile(ch => char.IsDigit(ch)).Count();
                name = originalName.Substring(0, name.IndexOf('`'));
                name += "<";
                int typeArgIndex = originalName.IndexOf('[', backTickIndex + 1);
                string typeArgumentsCountString = originalName.Substring(backTickIndex + 1, typeArgCountLength);
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

        internal static bool IsPublic(Type type, bool checkGenericTypeArgs = false)
        {
            Requires.NotNull(type, "type");

            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsNotPublic)
            {
                return false;
            }

            if (typeInfo.IsArray)
            {
                return IsPublic(typeInfo.GetElementType(), checkGenericTypeArgs);
            }

            if (checkGenericTypeArgs && typeInfo.IsGenericType && !typeInfo.IsGenericTypeDefinition)
            {
                if (typeInfo.GenericTypeArguments.Any(t => !IsPublic(t, true)))
                {
                    return false;
                }
            }

            if (typeInfo.IsPublic || typeInfo.IsNestedPublic)
            {
                return true;
            }

            return false;
        }

        internal static bool HasBaseclassOf(this Type type, Type baseClass)
        {
            if (type == baseClass)
            {
                return false;
            }

            while (type != null)
            {
                if (type == baseClass)
                    return true;
                type = type.GetTypeInfo().BaseType;
            }
            return false;
        }

        internal static bool IsEmbeddedType(this Type type)
        {
            Requires.NotNull(type, "type");
            var typeInfo = type.GetTypeInfo();

            if (typeInfo.IsInterface)
            {
                if (typeInfo.GetCustomAttribute<TypeIdentifierAttribute>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FilterTypeNameForGenericTypeDefinition(Type type, bool fullName)
        {
            Requires.NotNull(type, "type");

            string name = fullName ? type.FullName : type.Name;
            if (type.GetTypeInfo().IsGenericType && name.IndexOf('`') >= 0) // simple name may not include ` if parent type is the generic one
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

        private static Delegate CastAsFunc<T>(Func<object> func)
        {
            return new Func<T>(() => (T)func());
        }
    }
}
