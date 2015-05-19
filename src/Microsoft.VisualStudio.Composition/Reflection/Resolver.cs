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

    public static class Resolver
    {
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

            var manifest = constructorRef.Resolver.GetManifest(constructorRef.DeclaringType.AssemblyName);
            return (ConstructorInfo)manifest.ResolveMethod(constructorRef.MetadataToken);
        }

        public static MethodInfo Resolve(this MethodRef methodRef)
        {
            if (methodRef.IsEmpty)
            {
                return null;
            }

            var manifest = methodRef.Resolver.GetManifest(methodRef.DeclaringType.AssemblyName);
            var method = (MethodInfo)manifest.ResolveMethod(methodRef.MetadataToken);
            if (methodRef.GenericMethodArguments.Length > 0)
            {
                var constructedMethod = method.MakeGenericMethod(methodRef.GenericMethodArguments.Select(Resolve).ToArray());
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

            Type type = propertyRef.DeclaringType.Resolve();
            return type.GetRuntimeProperties().First(p => p.MetadataToken == propertyRef.MetadataToken);
        }

        public static MethodInfo ResolveGetter(this PropertyRef propertyRef)
        {
            if (propertyRef.GetMethodMetadataToken.HasValue)
            {
                Module manifest = propertyRef.Resolver.GetManifest(propertyRef.DeclaringType.AssemblyName);
                return (MethodInfo)manifest.ResolveMethod(propertyRef.GetMethodMetadataToken.Value);
            }

            return null;
        }

        public static MethodInfo ResolveSetter(this PropertyRef propertyRef)
        {
            if (propertyRef.SetMethodMetadataToken.HasValue)
            {
                Module manifest = propertyRef.Resolver.GetManifest(propertyRef.DeclaringType.AssemblyName);
                return (MethodInfo)manifest.ResolveMethod(propertyRef.SetMethodMetadataToken.Value);
            }

            return null;
        }

        public static FieldInfo Resolve(this FieldRef fieldRef)
        {
            if (fieldRef.IsEmpty)
            {
                return null;
            }

            var manifest = fieldRef.Resolver.GetManifest(fieldRef.AssemblyName);
            return manifest.ResolveField(fieldRef.MetadataToken);
        }

        public static ParameterInfo Resolve(this ParameterRef parameterRef)
        {
            if (parameterRef.IsEmpty)
            {
                return null;
            }

            Module manifest = parameterRef.Resolver.GetManifest(parameterRef.AssemblyName);
            MethodBase method = manifest.ResolveMethod(parameterRef.MethodMetadataToken);
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
                return memberRef.Field.Resolve();
            }

            if (memberRef.IsProperty)
            {
                return memberRef.Property.Resolve();
            }

            if (memberRef.IsMethod)
            {
                return memberRef.Method.Resolve();
            }

            if (memberRef.IsConstructor)
            {
                return memberRef.Constructor.Resolve();
            }

            if (memberRef.IsType)
            {
                return memberRef.Type.Resolve();
            }

            throw new NotSupportedException();
        }

        public static MemberInfo Resolve(this MemberDesc memberDesc)
        {
            var fieldDesc = memberDesc as FieldDesc;
            if (fieldDesc != null)
            {
                return fieldDesc.Field.Resolve();
            }

            var propertyDesc = memberDesc as PropertyDesc;
            if (propertyDesc != null)
            {
                return propertyDesc.Property.Resolve();
            }

            var methodDesc = memberDesc as MethodDesc;
            if (methodDesc != null)
            {
                return methodDesc.Method.Resolve();
            }

            var constructorDesc = memberDesc as ConstructorDesc;
            if (constructorDesc != null)
            {
                return constructorDesc.Constructor.Resolve();
            }

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
                    assemblies.Add(baseType.Assembly.GetName());
                }

                // Interfaces may also define [InheritedExport] attributes, metadata view filters, etc.
                foreach (var iface in type.GetInterfaces())
                {
                    assemblies.Add(iface.Assembly.GetName());
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
                assemblies.Add(propertyRef.DeclaringType.AssemblyName);
            }
        }

        internal static void GetInputAssemblies(this FieldRef fieldRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!fieldRef.IsEmpty)
            {
                assemblies.Add(fieldRef.DeclaringType.AssemblyName);
            }
        }

        internal static void GetInputAssemblies(this ConstructorRef constructorRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!constructorRef.IsEmpty)
            {
                assemblies.Add(constructorRef.DeclaringType.AssemblyName);
            }
        }

        internal static void GetInputAssemblies(this ParameterRef parameterRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            if (!parameterRef.IsEmpty)
            {
                assemblies.Add(parameterRef.AssemblyName);
            }
        }
    }

    public class MyResolver
    {
        /// <summary>
        /// A <see cref="MyResolver"/> instance that should only be used in code paths
        /// that serve for *debugging* purposes.
        /// </summary>
        public static readonly MyResolver DefaultInstance = new MyResolver(new StandardAssemblyLoader());

        /// <summary>
        /// A cache of TypeRef instances that correspond to Type instances.
        /// </summary>
        /// <remarks>
        /// This is for efficiency to avoid duplicates where convenient to do so.
        /// It is not intended as a guarantee of reference equality across equivalent TypeRef instances.
        /// </remarks>
        internal readonly Dictionary<Type, WeakReference<TypeRef>> InstanceCache = new Dictionary<Type, WeakReference<TypeRef>>();

        private readonly IAssemblyLoader assemblyLoader;

        private MyResolver(IAssemblyLoader assemblyLoader)
        {
            Requires.NotNull(assemblyLoader, nameof(assemblyLoader));
            this.assemblyLoader = assemblyLoader;
        }

        public static MyResolver Get(IAssemblyLoader assemblyLoader)
        {
            Requires.NotNull(assemblyLoader, nameof(assemblyLoader));

            return (assemblyLoader as MyResolver) ?? new MyResolver(assemblyLoader);
        }

        internal Module GetManifest(AssemblyName assemblyName)
        {
            Requires.NotNull(assemblyName, nameof(assemblyName));

            var assembly = this.assemblyLoader.LoadAssembly(assemblyName);
            return assembly.ManifestModule;
        }
    }
}
