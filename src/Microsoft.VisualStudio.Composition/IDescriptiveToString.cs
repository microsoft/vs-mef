// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MessagePack;

    [Union(0, typeof(ExportMetadataValueImportConstraint))]
    [Union(1, typeof(ExportTypeIdentityConstraint))]
    [Union(2, typeof(ImportMetadataViewConstraint))]
    [Union(3, typeof(PartCreationPolicyConstraint))]
    internal interface IDescriptiveToString
    {
        void ToString(TextWriter writer);
    }
}
