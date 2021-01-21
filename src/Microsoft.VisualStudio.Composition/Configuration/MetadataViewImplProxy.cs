// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using MefV1 = System.ComponentModel.Composition;

    internal class MetadataViewImplProxy : IMetadataViewProvider
    {
        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(MetadataViewImplProxy), 100, Resolver.DefaultInstance);

        public bool IsMetadataViewSupported(Type metadataType)
        {
            return HasMetadataViewImplementation(metadataType);
        }

        public object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType)
        {
            var ctor = FindImplClassConstructor(metadataViewType);
            Assumes.NotNull(ctor);
            return ctor.Invoke(new object[] { metadata });
        }

        internal static bool HasMetadataViewImplementation(Type metadataType)
        {
            return FindImplClassConstructor(metadataType) != null;
        }

        private static ConstructorInfo? FindImplClassConstructor(Type metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));
            var attr = metadataType.GetTypeInfo().GetFirstAttribute<MefV1.MetadataViewImplementationAttribute>();
            if (attr != null)
            {
                if (metadataType.IsAssignableFrom(attr.ImplementationType))
                {
                    var ctors = from ctor in attr.ImplementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                                let parameters = ctor.GetParameters()
                                where parameters.Length == 1 && (
                                    parameters[0].ParameterType.IsAssignableFrom(typeof(IDictionary<string, object?>)) ||
                                    parameters[0].ParameterType.IsAssignableFrom(typeof(IReadOnlyDictionary<string, object?>)))
                                select ctor;
                    return ctors.FirstOrDefault();
                }
            }

            return null;
        }
    }
}
