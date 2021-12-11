// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MefCalculator
{
    using System.Collections.Generic;
    using System.ComponentModel.Composition;

    [Export]
    public class ImportTest
    {
        [Import("MissingField")]
        private string? FailingField { get; set; }

        [ImportMany]
        public IEnumerable<IOperation>? Operations { get; set; }

        [Import]
        public int? IntInput { get; set; }
    }
}
