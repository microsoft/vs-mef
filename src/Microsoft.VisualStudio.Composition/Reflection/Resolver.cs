namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public static class Resolver
    {
        private static readonly Dictionary<AssemblyName, Module> assemblyManifests = new Dictionary<AssemblyName, Module>(ByValueEquality.AssemblyName);

        public static Type Resolve(this TypeRef typeRef)
        {
            return typeRef == null ? null : typeRef.ResolvedType;
        }

        public static ConstructorInfo Resolve(this ConstructorRef constructorRef)
        {
            if (constructorRef.IsEmpty)
            {
                return null;
            }

            var manifest = GetManifest(constructorRef.DeclaringType.AssemblyName);
            return (ConstructorInfo)manifest.ResolveMethod(constructorRef.MetadataToken);
        }

        public static MethodInfo Resolve(this MethodRef methodRef)
        {
            if (methodRef.IsEmpty)
            {
                return null;
            }

            var manifest = GetManifest(methodRef.DeclaringType.AssemblyName);
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

            Type type = Resolve(propertyRef.DeclaringType);
            return type.GetRuntimeProperties().First(p => p.MetadataToken == propertyRef.MetadataToken);
        }

        public static MethodInfo ResolveGetter(this PropertyRef propertyRef)
        {
            if (propertyRef.GetMethodMetadataToken.HasValue)
            {
                Module manifest = GetManifest(propertyRef.DeclaringType.AssemblyName);
                return (MethodInfo)manifest.ResolveMethod(propertyRef.GetMethodMetadataToken.Value);
            }

            return null;
        }

        public static MethodInfo ResolveSetter(this PropertyRef propertyRef)
        {
            if (propertyRef.SetMethodMetadataToken.HasValue)
            {
                Module manifest = GetManifest(propertyRef.DeclaringType.AssemblyName);
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

            var manifest = GetManifest(fieldRef.AssemblyName);
            return manifest.ResolveField(fieldRef.MetadataToken);
        }

        public static ParameterInfo Resolve(this ParameterRef parameterRef)
        {
            if (parameterRef.IsEmpty)
            {
                return null;
            }

            Module manifest = GetManifest(parameterRef.AssemblyName);
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

        internal static Module GetManifest(AssemblyName assemblyName)
        {
            Module module;
            lock (assemblyManifests)
            {
                assemblyManifests.TryGetValue(assemblyName, out module);
            }

            if (module == null)
            {
                var assembly = Assembly.Load(assemblyName);
                module = assembly.ManifestModule;

                lock (assemblyManifests)
                {
                    assemblyManifests[assemblyName] = module;
                }
            }

            return module;
        }

        internal static void GetInputAssemblies(this TypeRef typeRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, "assemblies");

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
                foreach (var baseType in typeRef.Resolve().EnumTypeAndBaseTypes())
                {
                    assemblies.Add(baseType.Assembly.GetName());
                }

                // Interfaces may also define [InheritedExport] attributes, metadata view filters, etc.
                foreach (var iface in typeRef.Resolve().GetInterfaces())
                {
                    assemblies.Add(iface.Assembly.GetName());
                }
            }
        }

        internal static void GetInputAssemblies(this MemberRef memberRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, "assemblies");

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
            Requires.NotNull(assemblies, "assemblies");

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
            Requires.NotNull(assemblies, "assemblies");

            if (!propertyRef.IsEmpty)
            {
                assemblies.Add(propertyRef.DeclaringType.AssemblyName);
            }
        }

        internal static void GetInputAssemblies(this FieldRef fieldRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, "assemblies");

            if (!fieldRef.IsEmpty)
            {
                assemblies.Add(fieldRef.DeclaringType.AssemblyName);
            }
        }

        internal static void GetInputAssemblies(this ConstructorRef constructorRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, "assemblies");

            if (!constructorRef.IsEmpty)
            {
                assemblies.Add(constructorRef.DeclaringType.AssemblyName);
            }
        }

        internal static void GetInputAssemblies(this ParameterRef parameterRef, ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, "assemblies");

            if (!parameterRef.IsEmpty)
            {
                assemblies.Add(parameterRef.AssemblyName);
            }
        }
    }
}
