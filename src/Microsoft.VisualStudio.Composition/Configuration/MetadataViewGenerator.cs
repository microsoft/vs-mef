// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/********************************************************
*                                                        *
*   © Copyright (C) Microsoft. All rights reserved.      *
*                                                        *
*********************************************************/

//// This file is originally derived from the version found in System.ComponentModel.Composition

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Security;
    using System.Threading;
    using Microsoft.VisualStudio.Composition.Reflection;

    /// <summary>
    /// Constructs concrete types for metadata view interfaces.
    /// </summary>
    /// <remarks>
    /// Assume TMetadataView is:
    /// <code><![CDATA[
    /// interface Foo
    /// {
    ///     public string RefTypeProperty { get; }
    ///     public bool ValueTypeProperty { get; }
    /// }
    /// ]]></code>
    ///
    /// The class to be generated will look approximately like:
    /// <code><![CDATA[
    /// public class __Foo__MetadataViewProxy : TMetadataView
    ///
    ///     private readonly IReadOnlyDictionary<string, object?> metadata;
    ///     private readonly IReadOnlyDictionary<string, object?> defaultMetadata;
    ///
    ///     private __Foo__MetadataViewProxy (IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultMetadata)
    ///     {
    ///         this.metadata = metadata;
    ///         this.defaultMetadata = defaultMetadata;
    ///     }
    ///
    ///     // Interface
    ///     public string RefTypeProperty
    ///     {
    ///         get
    ///         {
    ///             object value;
    ///             if (!this.metadata.TryGetValue("RefTypeProperty", out value))
    ///                 value = this.defaultMetadata["RefTypeProperty"];
    ///             return value as string;
    ///         }
    ///     }
    ///
    ///     public bool ValueTypeProperty
    ///     {
    ///         get
    ///         {
    ///             object value;
    ///             if (!this.metadata.TryGetValue("RefTypeProperty", out value))
    ///                 value = this.defaultMetadata["RefTypeProperty"];
    ///             return (bool)value;
    ///         }
    ///     }
    ///
    ///     public static object Create(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultMetadata)
    ///     {
    ///        return new __Foo__MetadataViewProxy(metadata, defaultMetadata);
    ///     }
    /// }
    /// ]]></code>
    /// </remarks>
    internal static class MetadataViewGenerator
    {
        private const string MetadataViewFactoryName = "Create";

        private static readonly Dictionary<Type, MetadataViewFactory> MetadataViewFactories = new Dictionary<Type, MetadataViewFactory>();

        private static readonly Type[] CtorArgumentTypes = new Type[] { typeof(IReadOnlyDictionary<string, object?>), typeof(IReadOnlyDictionary<string, object?>) };
        private static readonly MethodInfo MdvDictionaryTryGet = CtorArgumentTypes[0].GetTypeInfo().GetMethod("TryGetValue")!;
        private static readonly MethodInfo MdvDictionaryIndexer = CtorArgumentTypes[0].GetTypeInfo().GetMethod("get_Item")!;
        private static readonly MethodInfo ObjectGetType = typeof(object).GetTypeInfo().GetMethod("GetType", Type.EmptyTypes)!;
        private static readonly ConstructorInfo ObjectCtor = typeof(object).GetTypeInfo().GetConstructor(Type.EmptyTypes)!;

        private static readonly Dictionary<ImmutableHashSet<AssemblyName>, ModuleBuilder> TransparentProxyModuleBuilderByVisibilityCheck = new Dictionary<ImmutableHashSet<AssemblyName>, ModuleBuilder>(new ByContentEqualityComparer());

        public delegate object MetadataViewFactory(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultMetadata);

        private static AssemblyBuilder CreateProxyAssemblyBuilder()
        {
            var proxyAssemblyName = new AssemblyName(string.Format(CultureInfo.InvariantCulture, "MetadataViewProxies_{0}", Guid.NewGuid()));
            return AssemblyBuilder.DefineDynamicAssembly(proxyAssemblyName, AssemblyBuilderAccess.Run);
        }

        /// <summary>
        /// Gets the <see cref="ModuleBuilder"/> to use for generating a proxy for the given type.
        /// </summary>
        /// <param name="viewType">The type of the interface to generate a proxy for.</param>
        /// <returns>The <see cref="ModuleBuilder"/> to use.</returns>
        private static ModuleBuilder GetProxyModuleBuilder(TypeInfo viewType)
        {
            Requires.NotNull(viewType, nameof(viewType));
            Assumes.True(Monitor.IsEntered(MetadataViewFactories));

            // Dynamic assemblies are relatively expensive. We want to create as few as possible.
            // For each unique set of skip visibility check assemblies, we need a new dynamic assembly
            // because the CLR will not honor any additions to that set once the first generated type is closed.
            // So maintain a dictionary to point at dynamic modules based on the set of skip visiblity check assemblies they were generated with.
            var skipVisibilityCheckAssemblies = SkipClrVisibilityChecks.GetSkipVisibilityChecksRequirements(viewType);
            if (!TransparentProxyModuleBuilderByVisibilityCheck.TryGetValue(skipVisibilityCheckAssemblies, out ModuleBuilder? moduleBuilder))
            {
                var assemblyBuilder = CreateProxyAssemblyBuilder();
                moduleBuilder = assemblyBuilder.DefineDynamicModule("MetadataViewProxiesModule");
                var skipClrVisibilityChecks = new SkipClrVisibilityChecks(assemblyBuilder, moduleBuilder);
                skipClrVisibilityChecks.SkipVisibilityChecksFor(skipVisibilityCheckAssemblies);
                TransparentProxyModuleBuilderByVisibilityCheck.Add(skipVisibilityCheckAssemblies, moduleBuilder);
            }

            return moduleBuilder;
        }

        public static MetadataViewFactory GetMetadataViewFactory(Type viewType)
        {
            Assumes.NotNull(viewType);
            Assumes.True(viewType.GetTypeInfo().IsInterface);

            MetadataViewFactory? metadataViewFactory;

            lock (MetadataViewFactories)
            {
                if (!MetadataViewFactories.TryGetValue(viewType, out metadataViewFactory))
                {
                    // We actually create the proxy type within the lock because we're
                    // tampering with the ModuleBuilder which isn't thread-safe.
                    TypeInfo generatedProxyType = GenerateInterfaceViewProxyType(viewType);
                    var methodInfo = generatedProxyType.GetMethod(MetadataViewGenerator.MetadataViewFactoryName, BindingFlags.Public | BindingFlags.Static)!;
                    metadataViewFactory = (MetadataViewFactory)methodInfo.CreateDelegate(typeof(MetadataViewFactory));
                    MetadataViewFactories.Add(viewType, metadataViewFactory);
                }
            }

            return metadataViewFactory;
        }

        private static TypeInfo GenerateInterfaceViewProxyType(Type viewType)
        {
            // View type is an interface let's cook an implementation
            TypeInfo proxyType;
            TypeBuilder proxyTypeBuilder;
            Type[] interfaces = { viewType };

            var proxyModuleBuilder = GetProxyModuleBuilder(viewType.GetTypeInfo());
            proxyTypeBuilder = proxyModuleBuilder.DefineType(
                string.Format(CultureInfo.InvariantCulture, "_proxy_{0}_{1}", viewType.FullName, Guid.NewGuid()),
                TypeAttributes.Public,
                typeof(object),
                interfaces);

            // Generate field
            const string metadataFieldName = "metadata";
            FieldBuilder metadataFieldBuilder = proxyTypeBuilder.DefineField(
                metadataFieldName,
                CtorArgumentTypes[0],
                FieldAttributes.Private | FieldAttributes.InitOnly);
            const string metadataDefaultFieldName = "metadataDefault";
            FieldBuilder metadataDefaultFieldBuilder = proxyTypeBuilder.DefineField(
                metadataDefaultFieldName,
                CtorArgumentTypes[1],
                FieldAttributes.Private | FieldAttributes.InitOnly);

            // Implement Constructor
            ConstructorBuilder proxyCtor = proxyTypeBuilder.DefineConstructor(MethodAttributes.Private, CallingConventions.Standard, CtorArgumentTypes);
            ILGenerator proxyCtorIL = proxyCtor.GetILGenerator();

            // : base()
            proxyCtorIL.Emit(OpCodes.Ldarg_0);
            proxyCtorIL.Emit(OpCodes.Call, ObjectCtor);

            // this.metadata = metadata;
            proxyCtorIL.Emit(OpCodes.Ldarg_0);
            proxyCtorIL.Emit(OpCodes.Ldarg_1);
            proxyCtorIL.Emit(OpCodes.Stfld, metadataFieldBuilder);

            // this.metadataDefault = metadataDefault;
            proxyCtorIL.Emit(OpCodes.Ldarg_0);
            proxyCtorIL.Emit(OpCodes.Ldarg_2);
            proxyCtorIL.Emit(OpCodes.Stfld, metadataDefaultFieldBuilder);

            proxyCtorIL.Emit(OpCodes.Ret);

            foreach (PropertyInfo propertyInfo in viewType.GetAllProperties())
            {
                string propertyName = propertyInfo.Name;

                Type[] propertyTypeArguments = new Type[] { propertyInfo.PropertyType };
                Type[]? optionalModifiers = null;
                Type[]? requiredModifiers = null;

                // PropertyInfo does not support GetOptionalCustomModifiers and GetRequiredCustomModifiers on Silverlight
                optionalModifiers = propertyInfo.GetOptionalCustomModifiers();
                requiredModifiers = propertyInfo.GetRequiredCustomModifiers();
                Array.Reverse(optionalModifiers);
                Array.Reverse(requiredModifiers);

                // Generate property
                PropertyBuilder proxyPropertyBuilder = proxyTypeBuilder.DefineProperty(
                    propertyName,
                    PropertyAttributes.None,
                    propertyInfo.PropertyType,
                    propertyTypeArguments);

                // Generate "get" method implementation.
                MethodBuilder getMethodBuilder = proxyTypeBuilder.DefineMethod(
                    string.Format(CultureInfo.InvariantCulture, "get_{0}", propertyName),
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final,
                    CallingConventions.HasThis,
                    propertyInfo.PropertyType,
                    requiredModifiers,
                    optionalModifiers,
                    Type.EmptyTypes,
                    null,
                    null);

                proxyTypeBuilder.DefineMethodOverride(getMethodBuilder, propertyInfo.GetGetMethod()!);
                ILGenerator getMethodIL = getMethodBuilder.GetILGenerator();

                // object value;
                LocalBuilder valueLocal = getMethodIL.DeclareLocal(typeof(object));

                // this.metadata.TryGetValue(propertyName, out value);
                getMethodIL.Emit(OpCodes.Ldarg_0);
                getMethodIL.Emit(OpCodes.Ldfld, metadataFieldBuilder);
                getMethodIL.Emit(OpCodes.Ldstr, propertyName);
                getMethodIL.Emit(OpCodes.Ldloca_S, valueLocal);
                getMethodIL.Emit(OpCodes.Callvirt, MdvDictionaryTryGet);

                // If that succeeded, prepare to return.
                Label returnLabel = getMethodIL.DefineLabel();
                getMethodIL.Emit(OpCodes.Brtrue_S, returnLabel);

                // Otherwise get the value from the default metadata dictionary.
                getMethodIL.Emit(OpCodes.Ldarg_0);
                getMethodIL.Emit(OpCodes.Ldfld, metadataDefaultFieldBuilder);
                getMethodIL.Emit(OpCodes.Ldstr, propertyName);
                getMethodIL.Emit(OpCodes.Callvirt, MdvDictionaryIndexer);
                getMethodIL.Emit(OpCodes.Stloc_0);

                getMethodIL.MarkLabel(returnLabel);
                getMethodIL.Emit(OpCodes.Ldloc_0);
                getMethodIL.Emit(propertyInfo.PropertyType.GetTypeInfo().IsValueType ? OpCodes.Unbox_Any : OpCodes.Isinst, propertyInfo.PropertyType);
                getMethodIL.Emit(OpCodes.Ret);

                proxyPropertyBuilder.SetGetMethod(getMethodBuilder);
            }

            // Implement the static factory
            //// public static object Create(IReadOnlyDictionary<string, object?>, IReadOnlyDictionary<string, object?>)
            //// {
            ////    return new <ProxyClass>(dictionary);
            //// }
            MethodBuilder factoryMethodBuilder = proxyTypeBuilder.DefineMethod(MetadataViewGenerator.MetadataViewFactoryName, MethodAttributes.Public | MethodAttributes.Static, typeof(object), CtorArgumentTypes);
            ILGenerator factoryIL = factoryMethodBuilder.GetILGenerator();
            factoryIL.Emit(OpCodes.Ldarg_0);
            factoryIL.Emit(OpCodes.Ldarg_1);
            factoryIL.Emit(OpCodes.Newobj, proxyCtor);
            factoryIL.Emit(OpCodes.Ret);

            // Finished implementing the type
            proxyType = proxyTypeBuilder.CreateTypeInfo()!;

            return proxyType!;
        }

        private static IEnumerable<PropertyInfo> GetAllProperties(this Type type)
        {
            return type.GetTypeInfo().GetInterfaces().Concat(new Type[] { type }).SelectMany(itf => itf.GetTypeInfo().GetProperties());
        }

        private class ByContentEqualityComparer : IEqualityComparer<ImmutableHashSet<AssemblyName>>
        {
            public bool Equals(ImmutableHashSet<AssemblyName>? x, ImmutableHashSet<AssemblyName>? y)
            {
                if (x == y)
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                if (x.Count != y.Count)
                {
                    return false;
                }

                return !x.Except(y).Any();
            }

            public int GetHashCode(ImmutableHashSet<AssemblyName> obj)
            {
                int hashCode = 0;
                foreach (var item in obj)
                {
                    hashCode += item.GetHashCode();
                }

                return hashCode;
            }
        }
    }
}
