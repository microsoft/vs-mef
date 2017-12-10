// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Reflection;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal static class ExportFactory
    {
        private const string ExportFactoryV1FullName = "System.ComponentModel.Composition.ExportFactory";
        private const string ExportFactoryV2FullName = "System.Composition.ExportFactory";

        internal static bool IsExportFactoryType(this Type type)
        {
            return IsExportFactoryTypeV1(type) || IsExportFactoryTypeV2(type);
        }

        internal static bool IsExportFactoryType(this TypeRef type)
        {
            return IsExportFactoryTypeV1(type) || IsExportFactoryTypeV2(type);
        }

        internal static bool IsExportFactoryTypeV1(this Type type)
        {
            if (type != null && type.GetTypeInfo().IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.FullName.StartsWith(ExportFactoryV1FullName))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsExportFactoryTypeV1(this TypeRef type)
        {
            int arity = type?.GenericTypeParameterCount + type?.GenericTypeArguments.Length ?? 0;
            if (arity > 0 && arity <= 2)
            {
                if (type.FullName.StartsWith(ExportFactoryV1FullName))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsExportFactoryTypeV2(this Type type)
        {
            if (type != null && type.GetTypeInfo().IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.FullName.StartsWith(ExportFactoryV2FullName))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsExportFactoryTypeV2(this TypeRef type)
        {
            int arity = type?.GenericTypeParameterCount + type?.GenericTypeArguments.Length ?? 0;
            if (arity > 0 && arity <= 2)
            {
                if (type.FullName.StartsWith(ExportFactoryV2FullName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
