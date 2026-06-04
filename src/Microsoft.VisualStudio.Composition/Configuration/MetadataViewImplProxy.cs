// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using MefV1 = System.ComponentModel.Composition;

    internal class MetadataViewImplProxy : IMetadataViewProvider
    {
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

            var attr = metadataType.GetTypeInfo().GetFirstAttribute<MefV1.MetadataViewImplementationAttribute>();
            if (attr == null)
            {
                return null;
            }

            Type? implementationType = attr.ImplementationType;
            if (implementationType == null || !metadataType.IsAssignableFrom(implementationType))
            {
                if (throwOnInvalidConfiguration)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.MetadataViewImplementationTypeMustImplementMetadataView, implementationType?.FullName, metadataType.FullName));
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
                        && (parameters[0].ParameterType.IsAssignableFrom(typeof(IDictionary<string, object?>))
                        || parameters[0].ParameterType.IsAssignableFrom(typeof(IReadOnlyDictionary<string, object?>)));
                });
        }

        private readonly struct ImplementationActivation
        {
            internal ImplementationActivation(ImplementationKind kind, ConstructorInfo constructor)
            {
                this.Kind = kind;
                this.Constructor = constructor;
            }

            internal ImplementationKind Kind { get; }

            internal ConstructorInfo Constructor { get; }

            internal object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType)
            {
                Requires.NotNull(metadata, nameof(metadata));
                Requires.NotNull(defaultValues, nameof(defaultValues));
                Requires.NotNull(metadataViewType, nameof(metadataViewType));

                return this.Kind switch
                {
                    ImplementationKind.MetadataViewBase => InitializeMetadataView(metadata, defaultValues, this.Constructor),
                    ImplementationKind.LegacyDictionary => InvokeDictionaryConstructor(metadata, this.Constructor),
                    _ => throw Assumes.NotReachable(),
                };
            }

            private static object InitializeMetadataView(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, ConstructorInfo constructor)
            {
                var metadataView = (MetadataView)constructor.Invoke(Type.EmptyTypes);
                metadataView.Initialize(metadata, defaultValues);
                return metadataView;
            }

            private static object InvokeDictionaryConstructor(IReadOnlyDictionary<string, object?> metadata, ConstructorInfo constructor)
            {
                object metadataMaybeWrapped = constructor.GetParameters()[0].ParameterType.IsAssignableFrom(metadata.GetType()) ? metadata : ImmutableDictionary.CreateRange(metadata);
                return constructor.Invoke(new[] { metadataMaybeWrapped });
            }
        }
    }
}
