/********************************************************
*                                                        *
*   © Copyright (C) Microsoft. All rights reserved.      *
*                                                        *
*********************************************************/

// This file is originally derived from the version found in System.ComponentModel.Composition

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Security;
    using System.Threading;
    using Microsoft.Internal;
    using Validation;

    // // Assume TMetadataView is
    // //interface Foo
    // //{
    // //    public string RefTypeProperty { get; }
    // //    public bool ValueTypeProperty { get; }
    // //}
    // // The class to be generated will look approximately like:
    // public class __Foo__MedataViewProxy : TMetadataView
    // 
    //     private readonly IReadOnlyDictionary<string, object> metadata;
    //     private readonly IReadOnlyDictionary<string, object> defaultMetadata;
    //
    //     private __Foo__MedataViewProxy (IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultMetadata) 
    //     {
    //         this.metadata = metadata;
    //         this.defaultMetadata = defaultMetadata;
    //     }
    //
    //     // Interface
    //     public string RefTypeProperty
    //     {
    //         get
    //         {
    //             object value;
    //             if (!this.metadata.TryGetValue("RefTypeProperty", out value))
    //                 value = this.defaultMetadata["RefTypeProperty"];
    //             return value as string;
    //         }
    //     }
    //
    //     public bool ValueTypeProperty
    //     {
    //         get
    //         {
    //             object value;
    //             if (!this.metadata.TryGetValue("RefTypeProperty", out value))
    //                 value = this.defaultMetadata["RefTypeProperty"];
    //             return (bool)value;
    //         }
    //     }
    //
    //     public static object Create(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultMetadata) 
    //     {
    //        return new __Foo__MedataViewProxy(metadata, defaultMetadata);
    //     }
    // }
    internal static class MetadataViewGenerator
    {
        public delegate object MetadataViewFactory(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultMetadata);

        public const string MetadataViewType = "MetadataViewType";
        public const string MetadataItemKey = "MetadataItemKey";
        public const string MetadataItemTargetType = "MetadataItemTargetType";
        public const string MetadataItemSourceType = "MetadataItemSourceType";
        public const string MetadataItemValue = "MetadataItemValue";
        public const string MetadataViewFactoryName = "Create";

        private static readonly Dictionary<Type, MetadataViewFactory> metadataViewFactories = new Dictionary<Type, MetadataViewFactory>();
        private static readonly AssemblyName ProxyAssemblyName = new AssemblyName(string.Format(CultureInfo.InvariantCulture, "MetadataViewProxies_{0}", Guid.NewGuid()));
        private static ModuleBuilder transparentProxyModuleBuilder;

        private static readonly Type[] CtorArgumentTypes = new Type[] { typeof(IReadOnlyDictionary<string, object>), typeof(IReadOnlyDictionary<string, object>) };
        private static readonly MethodInfo mdvDictionaryTryGet = CtorArgumentTypes[0].GetMethod("TryGetValue");
        private static readonly MethodInfo mdvDictionaryIndexer = CtorArgumentTypes[0].GetMethod("get_Item");
        private static readonly MethodInfo ObjectGetType = typeof(object).GetMethod("GetType", Type.EmptyTypes);
        private static readonly ConstructorInfo ObjectCtor = typeof(object).GetConstructor(Type.EmptyTypes);

        private static AssemblyBuilder CreateProxyAssemblyBuilder(ConstructorInfo constructorInfo)
        {
            return AppDomain.CurrentDomain.DefineDynamicAssembly(ProxyAssemblyName, AssemblyBuilderAccess.Run);
        }

        private static ModuleBuilder GetProxyModuleBuilder()
        {
            Assumes.True(Monitor.IsEntered(metadataViewFactories));
            if (transparentProxyModuleBuilder == null)
            {
                // make a new assemblybuilder and modulebuilder
                var assemblyBuilder = CreateProxyAssemblyBuilder(typeof(SecurityTransparentAttribute).GetConstructor(Type.EmptyTypes));
                transparentProxyModuleBuilder = assemblyBuilder.DefineDynamicModule("MetadataViewProxiesModule");
            }

            return transparentProxyModuleBuilder;
        }

        public static MetadataViewFactory GetMetadataViewFactory(Type viewType)
        {
            Assumes.NotNull(viewType);
            Assumes.True(viewType.IsInterface);

            MetadataViewFactory metadataViewFactory;
            bool foundMetadataViewFactory;

            lock (metadataViewFactories)
            {
                foundMetadataViewFactory = metadataViewFactories.TryGetValue(viewType, out metadataViewFactory);

                // No factory exists
                if (!foundMetadataViewFactory)
                {
                    // Try again under a write lock if still none generate the proxy
                    Type generatedProxyType = GenerateInterfaceViewProxyType(viewType);
                    metadataViewFactory = (MetadataViewFactory)Delegate.CreateDelegate(
                        typeof(MetadataViewFactory), generatedProxyType.GetMethod(MetadataViewGenerator.MetadataViewFactoryName, BindingFlags.Public | BindingFlags.Static));
                    metadataViewFactories.Add(viewType, metadataViewFactory);
                }
            }

            return metadataViewFactory;
        }

        // This must be called with _readerWriterLock held for Write
        private static Type GenerateInterfaceViewProxyType(Type viewType)
        {
            // View type is an interface let's cook an implementation
            Type proxyType;
            TypeBuilder proxyTypeBuilder;
            Type[] interfaces = { viewType };

            var proxyModuleBuilder = GetProxyModuleBuilder();
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
                FieldAttributes.Private);
            const string metadataDefaultFieldName = "metadataDefault";
            FieldBuilder metadataDefaultFieldBuilder = proxyTypeBuilder.DefineField(
                metadataDefaultFieldName,
                CtorArgumentTypes[1],
                FieldAttributes.Private);

            // Implement Constructor
            ConstructorBuilder proxyCtor = proxyTypeBuilder.DefineConstructor(MethodAttributes.Private, CallingConventions.Standard, CtorArgumentTypes);
            ILGenerator proxyCtorIL = proxyCtor.GetILGenerator();
            proxyCtorIL.Emit(OpCodes.Ldarg_0);
            proxyCtorIL.Emit(OpCodes.Call, ObjectCtor);

            proxyCtorIL.Emit(OpCodes.Ldarg_0);
            proxyCtorIL.Emit(OpCodes.Ldarg_1);
            proxyCtorIL.Emit(OpCodes.Stfld, metadataFieldBuilder);

            proxyCtorIL.Emit(OpCodes.Ldarg_0);
            proxyCtorIL.Emit(OpCodes.Ldarg_2);
            proxyCtorIL.Emit(OpCodes.Stfld, metadataDefaultFieldBuilder);

            proxyCtorIL.Emit(OpCodes.Ret);

            foreach (PropertyInfo propertyInfo in viewType.GetAllProperties())
            {
                string propertyName = propertyInfo.Name;

                Type[] propertyTypeArguments = new Type[] { propertyInfo.PropertyType };
                Type[] optionalModifiers = null;
                Type[] requiredModifiers = null;

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
                    Type.EmptyTypes, null, null);

                proxyTypeBuilder.DefineMethodOverride(getMethodBuilder, propertyInfo.GetGetMethod());
                ILGenerator getMethodIL = getMethodBuilder.GetILGenerator();

                Label returnLabel = getMethodIL.DefineLabel();

                // object value;
                LocalBuilder valueLocal = getMethodIL.DeclareLocal(typeof(object));

                // this.metadata.TryGetValue(propertyName, out value);
                getMethodIL.Emit(OpCodes.Ldarg_0);
                getMethodIL.Emit(OpCodes.Ldfld, metadataFieldBuilder);
                getMethodIL.Emit(OpCodes.Ldstr, propertyName);
                getMethodIL.Emit(OpCodes.Ldloca_S, valueLocal);
                getMethodIL.Emit(OpCodes.Callvirt, mdvDictionaryTryGet);

                // If that succeeded, prepare to return.
                getMethodIL.Emit(OpCodes.Brtrue_S, returnLabel);

                // Otherwise get the value from the default metadata dictionary.
                getMethodIL.Emit(OpCodes.Ldarg_0);
                getMethodIL.Emit(OpCodes.Ldfld, metadataDefaultFieldBuilder);
                getMethodIL.Emit(OpCodes.Ldstr, propertyName);
                getMethodIL.Emit(OpCodes.Callvirt, mdvDictionaryIndexer);
                getMethodIL.Emit(OpCodes.Stloc_0);

                getMethodIL.MarkLabel(returnLabel);
                getMethodIL.Emit(OpCodes.Ldloc_0);
                getMethodIL.Emit(propertyInfo.PropertyType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Isinst, propertyInfo.PropertyType);
                getMethodIL.Emit(OpCodes.Ret);

                proxyPropertyBuilder.SetGetMethod(getMethodBuilder);
            }

            // Implement the static factory
            // public static object Create(IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>)
            // {
            //    return new <ProxyClass>(dictionary);
            // }
            MethodBuilder factoryMethodBuilder = proxyTypeBuilder.DefineMethod(MetadataViewGenerator.MetadataViewFactoryName, MethodAttributes.Public | MethodAttributes.Static, typeof(object), CtorArgumentTypes);
            ILGenerator factoryIL = factoryMethodBuilder.GetILGenerator();
            factoryIL.Emit(OpCodes.Ldarg_0);
            factoryIL.Emit(OpCodes.Ldarg_1);
            factoryIL.Emit(OpCodes.Newobj, proxyCtor);
            factoryIL.Emit(OpCodes.Ret);

            // Finished implementing the type
            proxyType = proxyTypeBuilder.CreateType();

            return proxyType;
        }
    }
}
