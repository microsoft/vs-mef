namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MefV1 = System.ComponentModel.Composition;

    internal static class ExportFactory
    {
        internal static bool IsExportFactoryTypeV1(this Type type)
        {
            if (type != null && type.IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.IsEquivalentTo(typeof(MefV1.ExportFactory<>)) || typeDefinition.IsEquivalentTo(typeof(MefV1.ExportFactory<,>)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsExportFactoryTypeV2(this Type type)
        {
            if (type != null && type.IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.IsEquivalentTo(typeof(ExportFactory<>)) || typeDefinition.IsEquivalentTo(typeof(ExportFactory<,>)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
