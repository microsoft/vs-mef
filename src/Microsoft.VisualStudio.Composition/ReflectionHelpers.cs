// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Reflection;

    public static class ReflectionHelpers
    {
        private static readonly Assembly Mscorlib = typeof(int).GetTypeInfo().Assembly;

        /// <summary>
        /// Describes how compatible an export and import site pair are.
        /// </summary>
        internal enum Assignability
        {
            /// <summary>
            /// Static analysis of the types involved guarantee that assignment will succeed at runtime.
            /// </summary>
            /// <remarks>
            /// For example, a property typed as string will always export a value assignable to an import of type string.
            /// </remarks>
            Definitely,

            /// <summary>
            /// Static analysis cannot definitively say whether assignment at runtime will succeed.
            /// </summary>
            /// <remarks>
            /// For example, a property typed as "object" that exports IFoo may return an IFoo object at runtime (success),
            /// or it may return a System.String object (failure).
            /// </remarks>
            Maybe,

            /// <summary>
            /// Static analysis of the types involved guarantee that assignment will fail at runtime.
            /// </summary>
            /// <remarks>
            /// For example, a property typed as string will never export a value assignable to an import of type int.
            /// </remarks>
            DefinitelyNot,
        }

        /// <summary>
        /// Creates a <see cref="Func{T}"/> delegate for a given <see cref="Func{Object}"/> delegate.
        /// </summary>
        /// <param name="typeArg">The <c>T</c> type argument for the returned function's return type.</param>
        /// <param name="func">The function that produces the T value typed as <see cref="object"/>.</param>
        /// <returns>An instance of <see cref="Func{T}"/>, typed as <see cref="Func{Object}"/>.</returns>
        public static Func<object> CreateFuncOfType(Type typeArg, Func<object> func)
        {
            return DelegateServices.As(func, typeArg);
        }

        internal static bool IsEquivalentTo(this Type type1, Type type2)
        {
            Requires.NotNull(type1, nameof(type1));
            Requires.NotNull(type2, nameof(type2));

            if (type1 == type2)
            {
                return true;
            }

            var type1Info = type1.GetTypeInfo();
            var type2Info = type2.GetTypeInfo();
            return type1Info.IsAssignableFrom(type2Info)
                && type2Info.IsAssignableFrom(type1Info);
        }

        internal static Assignability IsAssignableTo(ImportDefinitionBinding import, ExportDefinitionBinding export)
        {
            Requires.NotNull(import, nameof(import));
            Requires.NotNull(export, nameof(export));

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
                    return Assignability.Definitely;
                }
                catch (ArgumentException)
                {
                    return Assignability.DefinitelyNot;
                }
            }
            else
            {
                // Utilize the standard assignability checks for everything else.
                if (receivingType.GetTypeInfo().IsAssignableFrom(exportingType.GetTypeInfo()))
                {
                    return Assignability.Definitely;
                }

                bool valueTypeKnownExactly =
                    export.ExportingMemberRef.IsEmpty || // When [Export] appears on the type itself, we instantiate that exact type.
                    exportingType.GetTypeInfo().IsSealed;
                if (valueTypeKnownExactly)
                {
                    // There is no way that an exported value can implement the required types to make it assignable.
                    return Assignability.DefinitelyNot;
                }

                if (receivingType.GetTypeInfo().IsInterface || exportingType.GetTypeInfo().IsAssignableFrom(receivingType))
                {
                    // The actual exported value at runtime *may* be a derived type that *is* assignable to the import site.
                    return Assignability.Maybe;
                }

                return Assignability.DefinitelyNot;
            }
        }

        internal static ImmutableArray<TypeRef> GetParameterTypes(this MethodBase method, Resolver resolver)
        {
            Requires.NotNull(method, nameof(method));
            return method.GetParameters().Select(pi => TypeRef.Get(pi.ParameterType, resolver)).ToImmutableArray();
        }

        internal static ImmutableArray<TypeRef> GetGenericTypeArguments(this MethodBase method, Resolver resolver)
        {
            Requires.NotNull(method, nameof(method));
            return method.GetGenericArguments().Select(t => TypeRef.Get(t, resolver)).ToImmutableArray();
        }

        internal static IEnumerable<PropertyInfo> EnumProperties(this Type type)
        {
            Requires.NotNull(type, nameof(type));

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

        /// <summary>
        /// Produces a sequence of this type, and each of its base types, in order of ascending the type hierarchy.
        /// </summary>
        internal static IEnumerable<Type> EnumTypeAndBaseTypes(this Type type)
        {
            Requires.NotNull(type, nameof(type));

            while (type != null)
            {
                yield return type;
                type = type.GetTypeInfo().BaseType;
            }
        }

        internal static IEnumerable<PropertyInfo> WherePublicInstance(this IEnumerable<PropertyInfo> infos)
        {
            return infos.Where(p => p.GetMethod.IsPublicInstance() || p.SetMethod.IsPublicInstance());
        }

        internal static bool IsStatic(this MemberInfo exportingMember)
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
                return (exportingProperty.GetMethod ?? exportingProperty.SetMethod).IsStatic;
            }

            throw new NotSupportedException();
        }

        internal static Type GetMemberType(MemberInfo fieldOrPropertyOrType)
        {
            Requires.NotNull(fieldOrPropertyOrType, nameof(fieldOrPropertyOrType));

            var typeInfo = fieldOrPropertyOrType as TypeInfo;
            if (typeInfo != null)
            {
                return typeInfo.AsType();
            }

            var property = fieldOrPropertyOrType as PropertyInfo;
            if (property != null)
            {
                return property.PropertyType;
            }

            var field = fieldOrPropertyOrType as FieldInfo;
            if (field != null)
            {
                return field.FieldType;
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.UnexpectedMemberType, fieldOrPropertyOrType.MemberType));
        }

        internal static bool IsPublicInstance(this MethodInfo methodInfo)
        {
            return methodInfo.IsPublic && !methodInfo.IsStatic;
        }

        internal static string GetTypeName(Type type, bool genericTypeDefinition, bool evenNonPublic, HashSet<Assembly> relevantAssemblies, HashSet<Type> relevantEmbeddedTypes)
        {
            Requires.NotNull(type, nameof(type));

            if (type.IsArray)
            {
                return GetTypeName(type.GetElementType(), genericTypeDefinition, evenNonPublic, relevantAssemblies, relevantEmbeddedTypes) + "[]";
            }

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

            if (type.IsEquivalentTo(typeof(ValueType)))
            {
                return "object";
            }

            string result = string.Empty;
            if (type.DeclaringType != null)
            {
                // Take care to propagate generic type arguments to the declaring type.
                var declaringTypeInfo = type.DeclaringType.GetTypeInfo();
                var declaringType = declaringTypeInfo.ContainsGenericParameters && type.GenericTypeArguments.Length > declaringTypeInfo.GenericTypeArguments.Length
                    ? type.DeclaringType.MakeGenericType(type.GenericTypeArguments.Take(declaringTypeInfo.GenericTypeParameters.Length).ToArray())
                    : type.DeclaringType;
                result = GetTypeName(declaringType, genericTypeDefinition, evenNonPublic, relevantAssemblies, relevantEmbeddedTypes) + ".";
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

        private static void AddEmbeddedInterfaces(Type type, HashSet<Type> relevantEmbeddedTypes, ImmutableStack<Type> observedTypes = null)
        {
            Requires.NotNull(type, nameof(type));
            Requires.NotNull(relevantEmbeddedTypes, nameof(relevantEmbeddedTypes));

            observedTypes = observedTypes ?? ImmutableStack<Type>.Empty;
            if (observedTypes.Contains(type))
            {
                // avoid stackoverflow (when T implements IComparable<T>, for example).
                return;
            }

            observedTypes = observedTypes.Push(type);
            if (type.GetTypeInfo().Assembly != Mscorlib)
            {
                if (type.IsEmbeddedType())
                {
                    relevantEmbeddedTypes.Add(type);
                }

                if (type.GetTypeInfo().BaseType != null)
                {
                    AddEmbeddedInterfaces(type.GetTypeInfo().BaseType, relevantEmbeddedTypes, observedTypes);
                }

                foreach (Type iface in type.GetTypeInfo().ImplementedInterfaces)
                {
                    AddEmbeddedInterfaces(iface, relevantEmbeddedTypes, observedTypes);
                }
            }

            if (type.GetTypeInfo().IsGenericType)
            {
                foreach (Type typeArg in type.GenericTypeArguments)
                {
                    AddEmbeddedInterfaces(typeArg, relevantEmbeddedTypes, observedTypes);
                }
            }
        }

        internal static string ReplaceBackTickWithTypeArgs(string originalName, params string[] typeArguments)
        {
            Requires.NotNullOrEmpty(originalName, nameof(originalName));

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
                    Requires.Argument(typeArguments.Length == typeArgumentsCount, "typeArguments", Strings.WrongLength);
                    name += string.Join(",", typeArguments);
                }

                name += ">";
            }

            return name;
        }

        internal static bool IsPublic(Type type, bool checkGenericTypeArgs = false)
        {
            Requires.NotNull(type, nameof(type));

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
                // We have to treat embedded types that appear as generic type arguments as non-public,
                // because the CLR cannot assign Outer<TEmbedded> to Outer<TEmbedded> across assembly boundaries.
                if (typeInfo.GenericTypeArguments.Any(t => !IsPublic(t, true) || t.IsEmbeddedType()))
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
                {
                    return true;
                }

                type = type.GetTypeInfo().BaseType;
            }

            return false;
        }

        internal static bool IsEmbeddedType(this Type type)
        {
            Requires.NotNull(type, nameof(type));
            var typeInfo = type.GetTypeInfo();

            if (typeInfo.IsInterface)
            {
                // TypeIdentifierAttribute signifies an embeddED type.
                // ComImportAttribute suggests an embeddABLE type.
                if (typeInfo.IsAttributeDefined<TypeIdentifierAttribute>() && typeInfo.IsAttributeDefined<GuidAttribute>())
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsEmbeddableAssembly(this Assembly assembly)
        {
            Requires.NotNull(assembly, nameof(assembly));

            return assembly.GetCustomAttributes()
                .Any(a => a.GetType().FullName == "System.Runtime.InteropServices.PrimaryInteropAssemblyAttribute"
                    || a.GetType().FullName == "System.Runtime.InteropServices.ImportedFromTypeLibAttribute");
        }

        /// <summary>
        /// Returns a type with generic type arguments supplied by a constructed type that is derived from
        /// the supplied generic type definition.
        /// </summary>
        /// <param name="genericTypeDefinition">The generic type definition to return a constructed type from.</param>
        /// <param name="constructedType">A constructed type that is, or derives from, <paramref name="genericTypeDefinition"/>.</param>
        /// <returns>A constructed type.</returns>
        internal static Type CloseGenericType(Type genericTypeDefinition, Type constructedType)
        {
            using (var typeArguments = ExtractGenericTypeArguments(genericTypeDefinition, constructedType))
            {
                return genericTypeDefinition.MakeGenericType(typeArguments.Value);
            }
        }

        /// <summary>
        /// Extracts generic type arguments from a constructed type that are necessary to close a generic type definition.
        /// </summary>
        /// <param name="genericTypeDefinition">A generic type definition.</param>
        /// <param name="constructedType">A closed type from which may be obtained generic type arguments.</param>
        /// <returns>The type argument necessary to construct the closed type.</returns>
        internal static Rental<Type[]> ExtractGenericTypeArguments(Type genericTypeDefinition, Type constructedType)
        {
            Requires.NotNull(genericTypeDefinition, nameof(genericTypeDefinition));
            Requires.NotNull(constructedType, nameof(constructedType));

            var genericTypeDefinitionInfo = genericTypeDefinition.GetTypeInfo();

            // The generic type arguments may be buried in the base type of the "constructedType" that we were given.
            var constructedGenericType = constructedType;
            while (constructedGenericType != null && (!constructedGenericType.GetTypeInfo().IsGenericType || !genericTypeDefinitionInfo.IsAssignableFrom(constructedGenericType.GetGenericTypeDefinition().GetTypeInfo())))
            {
                constructedGenericType = constructedGenericType.GetTypeInfo().BaseType;
            }

            Requires.Argument(constructedGenericType != null, "constructedType", Strings.NotClosedFormOfOther);

            var result = ArrayRental<Type>.Get(genericTypeDefinitionInfo.GenericTypeParameters.Length);
            for (int i = 0; i < result.Value.Length; i++)
            {
                result.Value[i] = constructedGenericType.GenericTypeArguments[i];
            }

            return result;
        }

        internal static Type GetExportedValueType(Type declaringType, MemberInfo exportingMember)
        {
            if (exportingMember == null)
            {
                return declaringType;
            }

            if (exportingMember is FieldInfo || exportingMember is PropertyInfo)
            {
                return ReflectionHelpers.GetMemberType(exportingMember);
            }

            var exportingMethod = exportingMember as MethodInfo;
            if (exportingMethod != null)
            {
                return GetContractTypeForDelegate(exportingMethod);
            }

            throw new NotSupportedException();
        }

        internal static Type GetContractTypeForDelegate(MethodInfo method)
        {
            Requires.NotNull(method, nameof(method));

            ParameterInfo[] parameters = method.GetParameters();

            // This array should contains a lit of all argument types, and the last one is the return type (could be void)
            Type[] parameterTypes = new Type[parameters.Length + 1];
            parameterTypes[parameters.Length] = method.ReturnType;
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            return Expression.GetDelegateType(parameterTypes);
        }

        internal static Attribute Instantiate(this CustomAttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));

            Attribute attribute = (Attribute)attributeData.Constructor.Invoke(attributeData.ConstructorArguments.Select(ca => ca.Value).ToArray());
            foreach (var namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument.IsField)
                {
                    var field = attributeData.AttributeType.GetField(namedArgument.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Assumes.NotNull(field);
                    field.SetValue(attribute, namedArgument.TypedValue.Value);
                }
                else
                {
                    var property = attributeData.AttributeType.GetProperty(namedArgument.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Assumes.NotNull(property);
                    property.SetValue(attribute, namedArgument.TypedValue.Value);
                }
            }

            return attribute;
        }

        internal static void GetInputAssembliesFromMetadata(ISet<AssemblyName> assemblies, IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNull(assemblies, nameof(assemblies));
            Requires.NotNull(metadata, nameof(metadata));

            // Get the underlying metadata (should not load the assembly)
            metadata = LazyMetadataWrapper.TryUnwrap(metadata);
            foreach (var value in metadata.Values.Where(v => v != null))
            {
                var valueAsType = value as Type;
                var valueType = value.GetType();

                // Check lazy metadata first, then try to get the type data from the value (if not lazy)
                if (typeof(LazyMetadataWrapper.Enum32Substitution) == valueType)
                {
                    ((LazyMetadataWrapper.Enum32Substitution)value).EnumType.GetInputAssemblies(assemblies);
                }
                else if (typeof(LazyMetadataWrapper.TypeSubstitution) == valueType)
                {
                    ((LazyMetadataWrapper.TypeSubstitution)value).TypeRef.GetInputAssemblies(assemblies);
                }
                else if (typeof(LazyMetadataWrapper.TypeArraySubstitution) == valueType)
                {
                    foreach (var typeRef in ((LazyMetadataWrapper.TypeArraySubstitution)value).TypeRefArray)
                    {
                        typeRef.GetInputAssemblies(assemblies);
                    }
                }
                else if (valueAsType != null)
                {
                    GetTypeAndBaseTypeAssemblies(assemblies, valueAsType);
                }
                else if (value.GetType().IsArray)
                {
                    // If the value is an array, we should determine the assemblies of each item.
                    var array = value as object[];
                    if (array != null)
                    {
                        foreach (var obj in array.Where(o => o != null))
                        {
                            // Check to see if the value is a type. We should get the assembly from
                            // the value if that's the case.
                            var objType = obj as Type;
                            if (objType != null)
                            {
                                GetTypeAndBaseTypeAssemblies(assemblies, objType);
                            }
                            else
                            {
                                GetTypeAndBaseTypeAssemblies(assemblies, obj.GetType());
                            }
                        }
                    }
                    else
                    {
                        // Array is full of primitives. We can just use value's assembly data
                        GetTypeAndBaseTypeAssemblies(assemblies, value.GetType());
                    }
                }
                else
                {
                    GetTypeAndBaseTypeAssemblies(assemblies, value.GetType());
                }
            }
        }

        private static string FilterTypeNameForGenericTypeDefinition(Type type, bool fullName)
        {
            Requires.NotNull(type, nameof(type));

            string name = fullName ? type.FullName : type.Name;
            if (type.GetTypeInfo().IsGenericType && name.IndexOf('`') >= 0) // simple name may not include ` if parent type is the generic one
            {
                name = name.Substring(0, name.IndexOf('`'));
                name += "<";
                int genericPositions = Math.Max(type.GenericTypeArguments.Length, type.GetTypeInfo().GenericTypeParameters.Length);
                name += new string(',', genericPositions - 1);
                name += ">";
            }

            return name;
        }

        private static void GetTypeAndBaseTypeAssemblies(ISet<AssemblyName> assemblies, Type type)
        {
            Requires.NotNull(assemblies, nameof(assemblies));
            Requires.NotNull(type, nameof(type));

            foreach (var baseType in type.EnumTypeAndBaseTypes())
            {
                assemblies.Add(baseType.GetTypeInfo().Assembly.GetName());
            }

            foreach (var iface in type.GetTypeInfo().GetInterfaces())
            {
                assemblies.Add(iface.GetTypeInfo().Assembly.GetName());
            }
        }

        private static IEnumerable<Type> GetAllBaseTypesAndInterfaces(Type type)
        {
            Requires.NotNull(type, nameof(type));

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
