namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class CompositionConstants
    {
        internal const string PartCreationPolicyMetadataName = MefV1CompositionNamespace + ".CreationPolicy";
        internal const string GenericContractMetadataName = MefV1CompositionNamespace + ".GenericContractName";
        internal const string GenericParametersMetadataName = MefV1CompositionNamespace + ".GenericParameters";
        internal const string ExportTypeIdentityMetadataName = "ExportTypeIdentity";

        internal const string IsOpenGenericExport = MefV3CompositionNamespace + ".IsOpenGenericExport";

        private const string MefV1CompositionNamespace = "System.ComponentModel.Composition";
        private const string MefV3CompositionNamespace = "Microsoft.VisualStudio.Composition";
    }
}
