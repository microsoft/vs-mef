// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class ExportDefinitionTests
    {
        [Fact]
        public void EqualsConsidersMetadataWithArrays()
        {
            var exportDefinition1 = new ExportDefinition(
                "a",
                ImmutableDictionary.Create<string, object?>().Add("b", new object[] { "c" }));
            var exportDefinition2 = new ExportDefinition(
                "a",
                ImmutableDictionary.Create<string, object?>().Add("b", new object[] { "c" }));
            var exportDefinition3 = new ExportDefinition(
                "a",
                ImmutableDictionary.Create<string, object?>().Add("b", new object[] { "d" }));
            Assert.Equal(exportDefinition1, exportDefinition2);
            Assert.NotEqual(exportDefinition1, exportDefinition3);
        }
    }
}
