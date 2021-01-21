// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    public static class ResolverExtensions
    {
        private const BindingFlags AllInstanceMembers = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AllMembers = AllInstanceMembers | BindingFlags.Static;

        [return: NotNullIfNotNull("typeRef")]
        public static Type? Resolve(this TypeRef? typeRef)
        {
            return typeRef?.ResolvedType;
        }

        [return: NotNullIfNotNull("methodRef")]
        public static MethodBase? Resolve(this MethodRef? methodRef)
        {
            if (methodRef == null)
            {
                return null;
            }

            MethodBase? method = null;
            if (TryUseFastReflection(methodRef.DeclaringType, out Module manifest))
            {
                method = manifest.ResolveMethod(methodRef.MetadataToken);
            }
            else
            {
                TypeInfo declaringType = methodRef.DeclaringType.ResolvedType.GetTypeInfo();
                var candidates = methodRef.Name == ConstructorInfo.ConstructorName
                    ? (MethodBase[])declaringType.GetConstructors(AllInstanceMembers)
                    : declaringType.GetMethods(AllMembers);
                method = FindMethodByParameters(candidates, methodRef.Name, methodRef.ParameterTypes);
            }

            if (method is null)
            {
                throw new InvalidOperationException("Unable to find method: " + methodRef);
            }

            if (methodRef.GenericMethodArguments.Length > 0)
            {
                var constructedMethod = ((MethodInfo)method).MakeGenericMethod(methodRef.GenericMethodArguments.Select(Resolve).ToArray()!);
                return constructedMethod;
            }

            return method;
        }

        public static PropertyInfo? Resolve(this PropertyRef? propertyRef)
        {
            if (propertyRef == null)
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

        public static MethodInfo? ResolveGetter(this PropertyRef propertyRef)
        {
            if (propertyRef.GetMethodMetadataToken.HasValue)
            {
                if (TryUseFastReflection(propertyRef.DeclaringType, out Module manifest))
                {
                    return (MethodInfo)manifest.ResolveMethod(propertyRef.GetMethodMetadataToken.Value)!;
                }
                else
                {
                    return propertyRef.PropertyInfo.GetMethod;
                }
            }

            return null;
        }

        public static MethodInfo? ResolveSetter(this PropertyRef propertyRef)
        {
            if (propertyRef.SetMethodMetadataToken.HasValue)
            {
                if (TryUseFastReflection(propertyRef.DeclaringType, out Module manifest))
                {
                    return (MethodInfo)manifest.ResolveMethod(propertyRef.SetMethodMetadataToken.Value)!;
                }
                else
                {
                    return propertyRef.PropertyInfo.SetMethod;
                }
            }

            return null;
        }

        [return: NotNullIfNotNull("fieldRef")]
        public static FieldInfo? Resolve(this FieldRef? fieldRef)
        {
            if (fieldRef == null)
            {
                return null;
            }

            if (TryUseFastReflection(fieldRef.DeclaringType, out Module manifest))
            {
                return manifest.ResolveField(fieldRef.MetadataToken)!;
            }
            else
            {
                return Resolve(fieldRef.DeclaringType).GetField(fieldRef.Name, AllMembers)!;
            }
        }

        [return: NotNullIfNotNull("parameterRef")]
        public static ParameterInfo? Resolve(this ParameterRef? parameterRef)
        {
            if (parameterRef == null)
            {
                return null;
            }

            MethodBase method;
            if (TryUseFastReflection(parameterRef.DeclaringType, out Module manifest))
            {
                method = manifest.ResolveMethod(parameterRef.Method.MetadataToken)!;
            }
            else
            {
                method = parameterRef.Method.MethodBase;
            }

            return method.GetParameters()[parameterRef.ParameterIndex];
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

                assemblies.Add(typeRef.AssemblyName);

                // Base types may define [InheritedExport] attributes or otherwise influence MEF
                // so we should include them as input assemblies.
                if (!typeRef.IsShallow)
                {
                    foreach (var baseType in typeRef.BaseTypes)
                    {
                        assemblies.Add(baseType.AssemblyName);
                    }
                }
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
            manifest = typeRef.Resolver.GetManifest(typeRef.AssemblyName);
            return IsStrongAssemblyIdentityMatch(typeRef, manifest);
        }

        [return: MaybeNull]
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
