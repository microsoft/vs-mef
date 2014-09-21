namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    internal static class ExportFactory
    {
        internal static bool IsExportFactoryType(this Type type)
        {
            return IsExportFactoryTypeV1(type) || IsExportFactoryTypeV2(type);
        }

        internal static bool IsExportFactoryTypeV1(this Type type)
        {
            if (type != null && type.GetTypeInfo().IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.FullName.StartsWith("System.ComponentModel.Composition.ExportFactory"))
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
                if (typeDefinition.FullName.StartsWith("System.Composition.ExportFactory"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
