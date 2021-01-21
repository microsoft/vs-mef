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

    public class CardinalityMismatchTests
    {
        [Fact]
        public void MissingRequiredImport()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(new[] { TestUtilities.V2Discovery.CreatePart(typeof(RequiredImportMissing))! });
            var configuration = CompositionConfiguration.Create(catalog);
            Assert.Throws<CompositionFailedException>(() => configuration.ThrowOnErrors());
        }

        [MefFact(CompositionEngines.V2Compat, typeof(OptionalImportMissing))]
        public void MissingOptionalImport(IContainer container)
        {
            var export = container.GetExportedValue<OptionalImportMissing>();
            Assert.NotNull(export);
            Assert.Null(export.MissingOptionalImport);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonPublicOptionalImportMissing))]
        public void MissingOptionalImportNonPublic(IContainer container)
        {
            var export = container.GetExportedValue<NonPublicOptionalImportMissing>();
            Assert.NotNull(export);
            Assert.Null(export.MissingOptionalImport);
        }

        [Export]
        public class RequiredImportMissing
        {
            [Import]
            public ICustomFormatter MissingRequiredImport { get; set; } = null!;
        }

        [Export]
        public class OptionalImportMissing
        {
            [Import(AllowDefault = true)]
            public ICustomFormatter MissingOptionalImport { get; set; } = null!;
        }

        [MefV1.Export]
        public class NonPublicOptionalImportMissing
        {
            [MefV1.ImportingConstructor]
            internal NonPublicOptionalImportMissing([MefV1.Import(AllowDefault = true)] IFoo missingImport)
            {
            }

            [MefV1.Import(AllowDefault = true)]
            internal IFoo MissingOptionalImport { get; set; } = null!;
        }

        internal interface IFoo { }
    }
}
