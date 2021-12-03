// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MefCalculator
{
    using System.Collections.Generic;
    using System.Composition;

    public class ImportTest
    {
        [Import("MissingField")]
        public string? FailingField { get; }

        [Import("MetadataTest")]
        public int? IntInput { get; }

        [ImportMany]
        public IEnumerable<IOperation>? Operations { get; }
    }
}
