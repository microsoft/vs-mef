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
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class NonSharedPartTests
    {
        #region ImportManyOfNonSharedExportsActivatesPartJustOnce

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(EnumerableImportManyOfNonSharedPart), typeof(NonSharedPart))]
        public void ImportManyOfNonSharedExportsActivatesPartJustOnce(IContainer container)
        {
            var part = container.GetExportedValue<EnumerableImportManyOfNonSharedPart>();
            Assert.Same(part.NonSharedParts.Single(), part.NonSharedParts.Single());
            Assert.Same(part.LazyNonSharedParts.Single().Value, part.LazyNonSharedParts.Single().Value);
        }

        [Export, Shared]
        [MefV1.Export]
        public class EnumerableImportManyOfNonSharedPart
        {
            [ImportMany, MefV1.ImportMany]
            public IEnumerable<NonSharedPart> NonSharedParts { get; set; } = null!;

            [ImportMany, MefV1.ImportMany]
            public IEnumerable<Lazy<NonSharedPart>> LazyNonSharedParts { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPart { }

        #endregion

        #region Non-shared open generic export test

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(NonSharedOpenGenericPart<>))]
        public void GetExportedValuesOfNonSharedOpenGenericExportActivatesPartJustOnce(IContainer container)
        {
            var results = container.GetExportedValues<NonSharedOpenGenericPart<int>>();
            Assert.Same(results.Single(), results.Single());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(NonSharedOpenGenericPart<>))]
        public void GetExportsOfNonSharedOpenGenericExportActivatesPartJustOnce(IContainer container)
        {
            var results = container.GetExports<NonSharedOpenGenericPart<int>>();
            Assert.Same(results.Single().Value, results.Single().Value);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedOpenGenericPart<T> { }

        #endregion
    }
}
