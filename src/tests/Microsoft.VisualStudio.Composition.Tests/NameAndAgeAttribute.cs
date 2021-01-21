// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MefV1 = System.ComponentModel.Composition;

    [MetadataAttribute]
    [MefV1.MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class NameAndAgeAttribute : Attribute
    {
        public string? Name { get; set; }

        // TODO: make this an integer and verify tests still pass.
        public string? Age { get; set; }
    }
}
