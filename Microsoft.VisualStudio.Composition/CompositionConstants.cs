namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class CompositionConstants
    {
        internal const string PartCreationPolicyMetadataName = CompositionNamespace + ".CreationPolicy";
        internal const string GenericContractMetadataName = CompositionNamespace + ".GenericContractName";
        internal const string GenericParametersMetadataName = CompositionNamespace + ".GenericParameters";
        internal const string ExportTypeIdentityMetadataName = "ExportTypeIdentity";

        private const string CompositionNamespace = "System.ComponentModel.Composition";
    }
}
