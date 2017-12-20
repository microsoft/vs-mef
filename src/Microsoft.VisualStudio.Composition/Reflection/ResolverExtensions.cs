// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    public static class ResolverExtensions
    {
        private const BindingFlags AllInstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AllMembers = AllInstanceMembers | BindingFlags.Static;

        public static Type Resolve(this TypeRef typeRef)
        {
            return typeRef?.ResolvedType;
        }

        public static ConstructorInfo Resolve(this ConstructorRef constructorRef)
        {
            if (constructorRef.IsEmpty)
            {
                return null;
            }

#if RuntimeHandles
            if (TryUseFastReflection(constructorRef.DeclaringType, out Module manifest))
            {
                return (ConstructorInfo)manifest.ResolveMethod(constructorRef.MetadataToken);
            }
            else
#endif
            {
                return FindMethodByParameters(
                    Resolve(constructorRef.DeclaringType).GetConstructors(AllMembers),
                    ConstructorInfo.ConstructorName,
                    constructorRef.ParameterTypes);
            }
        }

        [Obsolete("Use Resolve2 instead.", error: true)]
        public static MethodInfo Resolve(this MethodRef methodRef) => (MethodInfo)Resolve2(methodRef);

        public static MethodBase Resolve2(this MethodRef methodRef)
        {
            if (methodRef.IsEmpty)
            {
                return null;
            }

            MethodBase method = null;
#if RuntimeHandles
            if (TryUseFastReflection(methodRef.DeclaringType, out Module manifest))
            {
                method = manifest.ResolveMethod(methodRef.MetadataToken);
            }
            else
#endif
            {
                TypeInfo declaringType = methodRef.DeclaringType.ResolvedType.GetTypeInfo();
                var candidates = methodRef.Name == ConstructorInfo.ConstructorName
                    ? (MethodBase[])declaringType.GetConstructors(AllInstanceMembers)
                    : declaringType.GetMethods(AllMembers);
                method = FindMethodByParameters(candidates, methodRef.Name, methodRef.ParameterTypes);
            }

            if (methodRef.GenericMethodArguments.Length > 0)
            {
                var constructedMethod = ((MethodInfo)method).MakeGenericMethod(methodRef.GenericMethodArguments.Select(Resolve).ToArray());
                return constructedMethod;
            }

            return method;
        }

        public static PropertyInfo Resolve(this PropertyRef propertyRef)
        {
            if (propertyRef.IsEmpty)
            {
                return null;
            }

            Type type = propertyRef.DeclaringType.ResolvedType;
            if (TryUseFastReflection(propertyRef.DeclaringType, out Module manifest))
            {
                return type.GetRuntimeProperties().First(p => p.MetadataToken == propertyRef.MetadataToken);
            }
            else
            {
                return type.GetProperty(propertyRef.Name, AllMembers);
            }
        }

        public static MethodInfo ResolveGetter(this PropertyRef propertyRef)
        {
            if (propertyRef.GetMethodMetadataToken.HasValue)
            {
#if RuntimeHandles
                if (TryUseFastReflection(propertyRef.DeclaringType, out Module manifest))
                {
                    return (MethodInfo)manifest.ResolveMethod(propertyRef.GetMethodMetadataToken.Value);
                }
                else
#endif
                {
                    return propertyRef.PropertyInfo.GetMethod;
                }
            }

            return null;
        }

        public static MethodInfo ResolveSetter(this PropertyRef propertyRef)
        {
            if (propertyRef.SetMethodMetadataToken.HasValue)
            {
#if RuntimeHandles
                if (TryUseFastReflection(propertyRef.DeclaringType, out Module manifest))
                {
                    return (MethodInfo)manifest.ResolveMethod(propertyRef.SetMethodMetadataToken.Value);
                }
                else
#endif
                {
                    return propertyRef.PropertyInfo.SetMethod;
                }
            }

            return null;
        }

        public static FieldInfo Resolve(this FieldRef fieldRef)
        {
            if (fieldRef.IsEmpty)
            {
                return null;
            }

#if RuntimeHandles
            if (TryUseFastReflection(fieldRef.DeclaringType, out Module manifest))
            {
                return manifest.ResolveField(fieldRef.MetadataToken);
            }
            else
#endif
            {
                return Resolve(fieldRef.DeclaringType).GetField(fieldRef.Name, AllMembers);
            }
        }

        public static ParameterInfo Resolve(this ParameterRef parameterRef)
        {
            if (parameterRef.IsEmpty)
            {
                return null;
            }

            MethodBase method;
#if RuntimeHandles
            if (TryUseFastReflection(parameterRef.DeclaringType, out Module manifest))
            {
                method = manifest.ResolveMethod(parameterRef.Constructor.IsEmpty ? parameterRef.Method.MetadataToken : parameterRef.Constructor.MetadataToken);
            }
            else
#endif
            {
                method = parameterRef.Constructor.ConstructorInfo ?? parameterRef.Method.MethodBase;
            }

            return method.GetParameters()[parameterRef.ParameterIndex];
        }

        public static MemberInfo Resolve(this MemberRef memberRef)
        {
            if (memberRef.IsEmpty)
            {
                return null;
            }

            if (memberRef.IsField)
            {
                return memberRef.Field.FieldInfo;
            }

            if (memberRef.IsProperty)
            {
                return memberRef.Property.PropertyInfo;
            }

            if (memberRef.IsMethod)
            {
                return memberRef.Method.MethodBase;
            }

            if (memberRef.IsConstructor)
            {
                return memberRef.Constructor.ConstructorInfo;
            }

            if (memberRef.IsType)
            {
                return memberRef.Type.ResolvedType.GetTypeInfo();
            }

            throw new NotSupportedException();
        }

        [Obsolete("Use " + nameof(MemberRef) + " instead.", error: true)]
        public static MemberInfo Resolve(this MemberDesc memberDesc)
        {
            throw new NotSupportedException();
        }

        internal static void GetInputAssemblies(this TypeRef typeRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (typeRef != null)
            {
                assemblies.Add(typeRef.AssemblyName);
                foreach (var typeArg in typeRef.GenericTypeArguments)
                {
                    GetInputAssemblies(typeArg, assemblies);
                }

                // Base types may define [InheritedExport] attributes or otherwise influence MEF
                // so we should include them as input assemblies.
                // Resolving a TypeRef is a necessary cost in order to identify the transitive closure of base types.
                var type = typeRef.Resolve();
                foreach (var baseType in type.EnumTypeAndBaseTypes())
                {
                    assemblies.Add(baseType.GetTypeInfo().Assembly.GetName());
                }

                // Interfaces may also define [InheritedExport] attributes, metadata view filters, etc.
                foreach (var iface in type.GetTypeInfo().GetInterfaces())
                {
                    assemblies.Add(iface.GetTypeInfo().Assembly.GetName());
                }
            }
        }

        internal static void GetInputAssemblies(this MemberRef memberRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (memberRef.IsConstructor)
            {
                GetInputAssemblies(memberRef.Constructor, assemblies);
            }
            else if (memberRef.IsField)
            {
                GetInputAssemblies(memberRef.Field, assemblies);
            }
            else if (memberRef.IsMethod)
            {
                GetInputAssemblies(memberRef.Method, assemblies);
            }
            else if (memberRef.IsProperty)
            {
                GetInputAssemblies(memberRef.Property, assemblies);
            }
        }

        internal static void GetInputAssemblies(this MethodRef methodRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!methodRef.IsEmpty)
            {
                assemblies.Add(methodRef.DeclaringType.AssemblyName);
                foreach (var typeArg in methodRef.GenericMethodArguments)
                {
                    GetInputAssemblies(typeArg, assemblies);
                }
            }
        }

        internal static void GetInputAssemblies(this PropertyRef propertyRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!propertyRef.IsEmpty)
            {
                propertyRef.DeclaringType.GetInputAssemblies(assemblies);
            }
        }

        internal static void GetInputAssemblies(this FieldRef fieldRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!fieldRef.IsEmpty)
            {
                fieldRef.DeclaringType.GetInputAssemblies(assemblies);
            }
        }

        internal static void GetInputAssemblies(this ConstructorRef constructorRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!constructorRef.IsEmpty)
            {
                constructorRef.DeclaringType.GetInputAssemblies(assemblies);
            }
        }

        internal static void GetInputAssemblies(this ParameterRef parameterRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!parameterRef.IsEmpty)
            {
                parameterRef.DeclaringType.GetInputAssemblies(assemblies);
            }
        }

        internal static Module GetManifest(this Resolver resolver, AssemblyName assemblyName)
        {
            return resolver.AssemblyLoader.LoadAssembly(assemblyName).ManifestModule;
        }

        /// <summary>
        /// Tests whether we can safely use fast reflection on the assembly that defines the given type.
        /// </summary>
        /// <param name="typeRef">The reference to a type that needs to be reflected over.</param>
        /// <param name="manifest">Receives the manifest of the assembly that defines the type. May be <c>null</c> when fast reflection must not be used.</param>
        /// <returns><c>true</c> if it is safe to use fast reflection; <c>false</c> otherwise.</returns>
        internal static bool TryUseFastReflection(TypeRef typeRef, out Module manifest)
        {
#if RuntimeHandles
            manifest = typeRef.Resolver.GetManifest(typeRef.AssemblyName);
            return IsStrongAssemblyIdentityMatch(typeRef, manifest);
#else
            manifest = null;
            return false;
#endif
        }

        private static T FindMethodByParameters<T>(IEnumerable<T> members, string memberName, ImmutableArray<TypeRef> parameterTypes)
            where T : MethodBase
        {
            Requires.NotNull(members, nameof(members));

            foreach (var member in members)
            {
                if (member.Name != memberName)
                {
                    continue;
                }

                var parameters = member.GetParameters();
                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (!parameterTypes[i].Equals(parameters[i].ParameterType))
                    {
                        continue;
                    }
                }

                return member;
            }

            return default(T);
        }

        /// <summary>
        /// Determines whether the metadata tokens stored in a <see cref="TypeRef"/>
        /// can be considered reliable considering the currently loaded assembly manifest.
        /// </summary>
        /// <param name="typeRef">The <see cref="TypeRef"/> that may have been cached, possibly against a different build of the assembly.</param>
        /// <param name="manifest">The manifest from the assembly that defines the referenced type.</param>
        /// <returns><c>true</c> if the currently loaded assembly is the same build as the one that was cached.</returns>
        private static bool IsStrongAssemblyIdentityMatch(TypeRef typeRef, Module manifest)
        {
            Requires.NotNull(typeRef, nameof(typeRef));
            Requires.NotNull(manifest, nameof(manifest));

            return typeRef.Resolver.GetStrongAssemblyIdentity(manifest.Assembly, typeRef.AssemblyName).Equals(typeRef.AssemblyId);
        }
    }
}
