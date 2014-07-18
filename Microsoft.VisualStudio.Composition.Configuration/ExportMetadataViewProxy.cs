namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Remoting.Messaging;
    using System.Runtime.Remoting.Proxies;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public static class ExportMetadataViewProxy
    {
        private static readonly ComposablePartDefinition proxySupportPartDefinition = new AttributedPartDiscovery().CreatePart(typeof(MetadataViewProxyProvider));

        public static ComposableCatalog WithMetadataViewProxySupport(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            return catalog.WithPart(proxySupportPartDefinition);
        }

        private static TMetadata GetProxy<TMetadata>(IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNull(metadata, "metadata");

            var proxy = new MetadataProxy<TMetadata>(metadata);
            return (TMetadata)proxy.GetTransparentProxy();
        }

        private class MetadataProxy<TMetadata> : RealProxy
        {
            private readonly IReadOnlyDictionary<string, object> metadata;

            internal MetadataProxy(IReadOnlyDictionary<string, object> metadata)
                : base(typeof(TMetadata))
            {
                Requires.NotNull(metadata, "metadata");
                this.metadata = metadata;
            }

            public override IMessage Invoke(IMessage msg)
            {
                var methodCall = (IMethodCallMessage)msg;

                string propertyName = methodCall.MethodName.Substring(4);

                object propertyValue = this.metadata[propertyName];
                return new ReturnMessage(propertyValue, null, 0, methodCall.LogicalCallContext, methodCall);
            }
        }

        [PartNotDiscoverable]
        [Export(typeof(IMetadataViewProvider))]
        private class MetadataViewProxyProvider : IMetadataViewProvider
        {
            public bool IsMetadataViewSupported(Type metadataType)
            {
                if (metadataType.IsInterface &&
                    metadataType.GetMembers().All(IsPropertyRelated))
                {
                    return true;
                }

                return false;
            }

            public TMetadata CreateProxy<TMetadata>(IReadOnlyDictionary<string, object> metadata)
            {
                return GetProxy<TMetadata>(metadata);
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
