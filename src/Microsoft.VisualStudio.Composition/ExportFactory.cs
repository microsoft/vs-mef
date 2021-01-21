// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
                if (typeDefinition.FullName?.StartsWith(ExportFactoryV1FullName) ?? false)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsExportFactoryTypeV1(this TypeRef type)
        {
            if (type is object)
            {
                int arity = type.GenericTypeParameterCount + type.GenericTypeArguments.Length;
                if (arity > 0 && arity <= 2)
                {
                    if (type.FullName?.StartsWith(ExportFactoryV1FullName) ?? false)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool IsExportFactoryTypeV2(this Type type)
        {
            if (type != null && type.GetTypeInfo().IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.FullName?.StartsWith(ExportFactoryV2FullName) ?? false)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsExportFactoryTypeV2(this TypeRef type)
        {
            if (type is object)
            {
                int arity = type.GenericTypeParameterCount + type.GenericTypeArguments.Length;
                if (arity > 0 && arity <= 2)
                {
                    if (type.FullName?.StartsWith(ExportFactoryV2FullName) ?? false)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
