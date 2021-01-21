// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class CompositionConstants
    {
        internal const string DgmlCategoryPartMetadataName = "VsMEFDgmlCategories";

        internal const string PartCreationPolicyMetadataName = MefV1CompositionNamespace + ".CreationPolicy";
        internal const string GenericContractMetadataName = MefV1CompositionNamespace + ".GenericContractName";
        public const string GenericParametersMetadataName = MefV1CompositionNamespace + ".GenericParameters";
        internal const string ExportTypeIdentityMetadataName = "ExportTypeIdentity";

        internal const string IsOpenGenericExport = MefV3CompositionNamespace + ".IsOpenGenericExport";

        // ExportFactory<T> support (V3-specific)
        internal const string ExportFactoryProductImportDefinition = MefV3CompositionNamespace + ".ProductImportDefinition";
        internal const string ExportFactoryTypeMetadataName = MefV3CompositionNamespace + ".ExportFactoryType";

        // ExportFactory<T> support (copied from V1)
        internal const string ProductDefinitionMetadataName = "ProductDefinition";
        internal const string PartCreatorContractName = MefV1CompositionNamespace + ".Contracts.ExportFactory";

        private const string MefV1CompositionNamespace = "System.ComponentModel.Composition";
        private const string MefV3CompositionNamespace = "Microsoft.VisualStudio.Composition";
    }
}
