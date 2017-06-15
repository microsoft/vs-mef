// Copyright (c) Microsoft. All rights reserved.

#if NET45

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.Composition.ReflectionModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Reflection;
    using MefV1 = System.ComponentModel.Composition;

    internal class MetadataViewImplProxy : IMetadataViewProvider
    {
        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(MetadataViewImplProxy), 100, Resolver.DefaultInstance);

        public bool IsMetadataViewSupported(Type metadataType)
        {
            return FindImplClassConstructor(metadataType) != null;
        }

        public object CreateProxy(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultValues, Type metadataViewType)
        {
            var ctor = FindImplClassConstructor(metadataViewType);
            return ctor.Invoke(new object[] { metadata });
        }

        private static ConstructorInfo FindImplClassConstructor(Type metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));
            var attr = metadataType.GetFirstAttribute<MefV1.MetadataViewImplementationAttribute>();
            if (attr != null)
            {
                if (metadataType.IsAssignableFrom(attr.ImplementationType))
                {
                    var ctors = from ctor in attr.ImplementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                                let parameters = ctor.GetParameters()
                                where parameters.Length == 1 && (
                                    parameters[0].ParameterType.IsAssignableFrom(typeof(IDictionary<string, object>)) ||
                                    parameters[0].ParameterType.IsAssignableFrom(typeof(IReadOnlyDictionary<string, object>)))
                                select ctor;
                    return ctors.FirstOrDefault();
                }
            }

            return null;
        }
    }
}

#endif
