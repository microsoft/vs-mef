// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using MefV1 = System.ComponentModel.Composition;

    internal class MetadataViewImplProxy : IMetadataViewProvider
    {
        private static readonly ConcurrentDictionary<Type, ImplementationActivation> ImplementationActivations = new ConcurrentDictionary<Type, ImplementationActivation>();
        private static readonly ConcurrentDictionary<(Type MetadataType, Type ImplementationType), ImplementationActivation> ExplicitImplementationActivations = new ConcurrentDictionary<(Type MetadataType, Type ImplementationType), ImplementationActivation>();
        private static readonly MethodInfo CreateMetadataViewMethod = typeof(MetadataViewImplProxy).GetMethod(nameof(CreateMetadataView), BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(MetadataViewImplProxy), 100, Resolver.DefaultInstance);

        internal enum ImplementationKind
        {
            None,
            LegacyDictionary,
            MetadataViewBase,
        }

        public bool IsMetadataViewSupported(Type metadataType)
        {
            return TryGetImplementationActivation(metadataType, throwOnInvalidConfiguration: true) is object;
        }

        public object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType)
        {
            var activation = TryGetImplementationActivation(metadataViewType, throwOnInvalidConfiguration: true);
            Assumes.NotNull(activation);
            return activation.Value.CreateProxy(metadata, defaultValues, metadataViewType);
        }

        internal static bool IsMetadataViewImplementationSupported(Type metadataType, Type implementationType, bool throwOnInvalidConfiguration = true)
        {
            return TryGetImplementationActivation(metadataType, implementationType, throwOnInvalidConfiguration) is object;
        }

        internal static object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType, Type implementationType)
        {
            var activation = TryGetImplementationActivation(metadataViewType, implementationType, throwOnInvalidConfiguration: true);
            Assumes.NotNull(activation);
            return activation.Value.CreateProxy(metadata, defaultValues, metadataViewType);
        }

        internal static bool HasMetadataViewImplementation(Type metadataType)
        {
            return GetImplementationKind(metadataType) != ImplementationKind.None;
        }

        internal static ImplementationKind GetImplementationKind(Type metadataType)
        {
            return TryGetImplementationActivation(metadataType, throwOnInvalidConfiguration: true)?.Kind ?? ImplementationKind.None;
        }

        private static ImplementationActivation? TryGetImplementationActivation(Type metadataType, bool throwOnInvalidConfiguration)
        {
            Requires.NotNull(metadataType, nameof(metadataType));

            if (ImplementationActivations.TryGetValue(metadataType, out ImplementationActivation cachedActivation))
            {
                return cachedActivation;
            }

            Type? implementationType = GetMetadataViewImplementationType(metadataType);
            if (implementationType is null)
            {
                return null;
            }

            ImplementationActivation? activation = TryGetImplementationActivation(metadataType, implementationType, throwOnInvalidConfiguration);
            if (activation.HasValue)
            {
                return ImplementationActivations.GetOrAdd(metadataType, activation.Value);
            }

            return null;
        }

        private static Type? GetMetadataViewImplementationType(Type metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));

            var attr = metadataType.GetTypeInfo().GetFirstAttribute<MefV1.MetadataViewImplementationAttribute>();
            return attr?.ImplementationType;
        }

        private static ImplementationActivation? TryGetImplementationActivation(Type metadataType, Type implementationType, bool throwOnInvalidConfiguration)
        {
            Requires.NotNull(metadataType, nameof(metadataType));
            Requires.NotNull(implementationType, nameof(implementationType));

            var key = (MetadataType: metadataType, ImplementationType: implementationType);
            if (ExplicitImplementationActivations.TryGetValue(key, out ImplementationActivation cachedActivation))
            {
                return cachedActivation;
            }

            ImplementationActivation? activation = CreateImplementationActivation(metadataType, implementationType, throwOnInvalidConfiguration);
            if (activation.HasValue)
            {
                return ExplicitImplementationActivations.GetOrAdd(key, activation.Value);
            }

            return null;
        }

        private static ImplementationActivation? CreateImplementationActivation(Type metadataType, Type implementationType, bool throwOnInvalidConfiguration)
        {
            if (!metadataType.IsAssignableFrom(implementationType))
            {
                if (throwOnInvalidConfiguration)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.MetadataViewImplementationTypeMustImplementMetadataView, implementationType.FullName, metadataType.FullName));
                }

                return null;
            }

            TypeInfo implementationTypeInfo = implementationType.GetTypeInfo();

            ConstructorInfo? ctor = FindDefaultConstructor(implementationTypeInfo);
            if (ctor != null && typeof(MetadataView).GetTypeInfo().IsAssignableFrom(implementationTypeInfo))
            {
                return new ImplementationActivation(ImplementationKind.MetadataViewBase, ctor);
            }

            ctor = FindDictionaryConstructor(implementationTypeInfo);
            if (ctor != null)
            {
                return new ImplementationActivation(ImplementationKind.LegacyDictionary, ctor);
            }

            if (throwOnInvalidConfiguration)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.MetadataViewImplementationConstructorUnsupported, implementationType.FullName, metadataType.FullName));
            }

            return null;
        }

        private static MetadataViewFactory CreateMetadataViewFactory(Type implementationType)
        {
            MethodInfo factoryMethod = CreateMetadataViewMethod.MakeGenericMethod(implementationType);
            return (MetadataViewFactory)factoryMethod.CreateDelegate(typeof(MetadataViewFactory));
        }

        private static MetadataView CreateMetadataView<T>()
            where T : MetadataView, new()
        {
            return new T();
        }

        private static ConstructorInfo? FindDefaultConstructor(TypeInfo implementationType)
        {
            Requires.NotNull(implementationType, nameof(implementationType));

            return implementationType.DeclaredConstructors.FirstOrDefault(ctor => ctor.IsPublic && !ctor.IsStatic && ctor.GetParameters().Length == 0);
        }

        private static ConstructorInfo? FindDictionaryConstructor(TypeInfo implementationType)
        {
            Requires.NotNull(implementationType, nameof(implementationType));

            return implementationType.DeclaredConstructors.FirstOrDefault(
                ctor =>
                {
                    if (!ctor.IsPublic || ctor.IsStatic)
                    {
                        return false;
                    }

                    var parameters = ctor.GetParameters();
                    return parameters.Length == 1
                        && IsSupportedDictionaryParameterType(parameters[0].ParameterType);
                });
        }

        private static bool IsSupportedDictionaryParameterType(Type parameterType)
        {
            return (typeof(IDictionary<string, object?>).IsAssignableFrom(parameterType)
                || typeof(IReadOnlyDictionary<string, object?>).IsAssignableFrom(parameterType))
                && (parameterType.IsAssignableFrom(typeof(Dictionary<string, object?>))
                || parameterType.IsAssignableFrom(typeof(ImmutableDictionary<string, object?>)));
        }

        private delegate MetadataView MetadataViewFactory();

        private readonly struct ImplementationActivation
        {
            private readonly ConstructorInfo? constructor;
            private readonly Type? constructorParameterType;
            private readonly MetadataViewFactory? metadataViewFactory;

            internal ImplementationActivation(ImplementationKind kind, ConstructorInfo constructor)
            {
                this.Kind = kind;
                this.constructor = kind == ImplementationKind.LegacyDictionary ? constructor : null;
                this.constructorParameterType = kind == ImplementationKind.LegacyDictionary ? constructor.GetParameters()[0].ParameterType : null;
                this.metadataViewFactory = kind == ImplementationKind.MetadataViewBase ? CreateMetadataViewFactory(constructor.DeclaringType!) : null;
            }

            internal ImplementationKind Kind { get; }

            internal object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType)
            {
                Requires.NotNull(metadata, nameof(metadata));
                Requires.NotNull(defaultValues, nameof(defaultValues));
                Requires.NotNull(metadataViewType, nameof(metadataViewType));

                return this.Kind switch
                {
                    ImplementationKind.MetadataViewBase => InitializeMetadataView(metadata, defaultValues, this.metadataViewFactory!),
                    ImplementationKind.LegacyDictionary => InvokeDictionaryConstructor(metadata, this.constructor!, this.constructorParameterType!),
                    _ => throw Assumes.NotReachable(),
                };
            }

            private static object InitializeMetadataView(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, MetadataViewFactory metadataViewFactory)
            {
                MetadataView metadataView = metadataViewFactory();
                metadataView.Initialize(metadata, defaultValues);
                return metadataView;
            }

            private static object InvokeDictionaryConstructor(IReadOnlyDictionary<string, object?> metadata, ConstructorInfo constructor, Type parameterType)
            {
                object metadataMaybeWrapped = parameterType.IsInstanceOfType(metadata)
                    ? metadata
                    : parameterType.IsAssignableFrom(typeof(Dictionary<string, object?>))
                    ? metadata.ToDictionary(pair => pair.Key, pair => pair.Value)
                    : parameterType.IsAssignableFrom(typeof(ImmutableDictionary<string, object?>))
                    ? ImmutableDictionary.CreateRange(metadata)
                    : throw Assumes.NotReachable();
                return constructor.Invoke(new[] { metadataMaybeWrapped });
            }
        }
    }
}
