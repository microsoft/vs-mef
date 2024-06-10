// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.VisualStudio.Composition.Formatter;
using static Microsoft.VisualStudio.Composition.NetFxAdapters;

[Union(0, typeof(ImportConstraint))]
[Union(1, typeof(ExportMetadataValueImportConstraint))]
[Union(2, typeof(ExportTypeIdentityConstraint))]
[Union(3, typeof(ImportMetadataViewConstraint))]
[Union(4, typeof(PartCreationPolicyConstraint))]
public interface IImportSatisfiabilityConstraint : IEquatable<IImportSatisfiabilityConstraint>
{
    bool IsSatisfiedBy(ExportDefinition exportDefinition);
}
