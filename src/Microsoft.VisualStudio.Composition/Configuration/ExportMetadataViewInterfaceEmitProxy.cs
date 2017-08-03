// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using Reflection;

    internal static class ExportMetadataViewInterfaceEmitProxy
    {
        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(MetadataViewProxyProvider), 50, Resolver.DefaultInstance);

#pragma warning disable CS3016 // Arrays as attribute arguments is not CLS compliant. But these are private so we don't care.

        [PartNotDiscoverable]
        [PartMetadata(CompositionConstants.DgmlCategoryPartMetadataName, new string[] { "VsMEFBuiltIn" })]
        [Export(typeof(IMetadataViewProvider))]
        [ExportMetadata("OrderPrecedence", 50)]
        private class MetadataViewProxyProvider : IMetadataViewProvider
        {
            public bool IsMetadataViewSupported(Type metadataType)
            {
                // We apply to interfaces with nothing but property getters.
                return metadataType.GetTypeInfo().IsInterface
                    && metadataType.GetTypeInfo().GetMembers().All(IsPropertyRelated);
            }

            public object CreateProxy(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultValues, Type metadataViewType)
            {
                var factory = MetadataViewGenerator.GetMetadataViewFactory(metadataViewType);
                return factory(metadata, defaultValues);
            }

            private static bool IsPropertyRelated(MemberInfo member)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    return property.GetMethod != null && property.SetMethod == null;
                }

                var method = member as MethodInfo;
                if (method != null)
                {
                    return method.IsSpecialName
                        && method.Name.StartsWith("get_");
                }

                return false;
            }
        }
    }
}
