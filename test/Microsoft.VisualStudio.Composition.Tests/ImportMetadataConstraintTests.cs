// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class ImportMetadataConstraintTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void ImportOneWithConstraint(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.IsType<ExportFirst>(part.FirstByOne);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyWithConstraintToOne(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.IsType<ExportSecond>(part.SecondByMany.Single());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyWithConstraintToTwo(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.Equal(2, part.OddNumberedExports.Count);
            Assert.Single(part.OddNumberedExports.OfType<ExportFirst>());
            Assert.Single(part.OddNumberedExports.OfType<ExportThird>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyUnconstrained(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithContraintPart>();
            Assert.Equal(3, part.UnconstrainedMany.Count);
        }

        [Fact]
        public async Task TypeArrayConstraintIsPreservedAfterCatalogDeserializationAsync()
        {
            TypeRef partType = TypeRef.Get(typeof(ImportWithTypeArrayConstraint), TestUtilities.Resolver);
            TypeRef objectType = TypeRef.Get(typeof(object), TestUtilities.Resolver);
            var sourceConstraint = new ExportMetadataValueImportConstraint("Types", new Type[] { typeof(string), typeof(int) });
            var importDefinition = new ImportDefinition(
                "Contract",
                ImportCardinality.ExactlyOne,
                ImmutableDictionary<string, object?>.Empty,
                ImmutableHashSet.Create<IImportSatisfiabilityConstraint>(sourceConstraint));
            var importBinding = new ImportDefinitionBinding(
                importDefinition,
                partType,
                new PropertyRef(typeof(ImportWithTypeArrayConstraint).GetProperty(nameof(ImportWithTypeArrayConstraint.Value))!, TestUtilities.Resolver),
                objectType,
                objectType);
            var part = new ComposablePartDefinition(
                partType,
                ImmutableDictionary<string, object?>.Empty,
                ImmutableList<ExportDefinition>.Empty,
                ImmutableDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>.Empty,
                ImmutableList.Create(importBinding),
                sharingBoundary: null,
                ImmutableList<MethodRef>.Empty,
                importingConstructorRef: null,
                importingConstructorImports: null,
                CreationPolicy.Any);
            ComposableCatalog catalog = ComposableCatalog.Create(TestUtilities.Resolver).AddPart(part);
            var cache = new CachedCatalog();
            using var stream = new MemoryStream();
            await cache.SaveAsync(catalog, stream);

            stream.Position = 0;
            ComposableCatalog deserializedCatalog = await cache.LoadAsync(stream, TestUtilities.Resolver);
            ExportMetadataValueImportConstraint deserializedConstraint = deserializedCatalog.Parts
                .Single()
                .ImportingMembers
                .Single()
                .ImportDefinition
                .ExportConstraints
                .OfType<ExportMetadataValueImportConstraint>()
                .Single();

            Assert.Equal(new Type[] { typeof(string), typeof(int) }, Assert.IsType<Type[]>(deserializedConstraint.Value));
        }

        [Export]
        public class ImportOneWithContraintPart
        {
            [Import("Common"), ImportMetadataConstraint("Name", "First")]
            public object FirstByOne { get; set; } = null!;

            [ImportMany("Common"), ImportMetadataConstraint("Name", "Second")]
            public ICollection<object> SecondByMany { get; set; } = null!;

            [ImportMany("Common"), ImportMetadataConstraint("Number", "Odd")]
            public ICollection<object> OddNumberedExports { get; set; } = null!;

            [ImportMany("Common")]
            public ICollection<object> UnconstrainedMany { get; set; } = null!;
        }

        private class ImportWithTypeArrayConstraint
        {
            public object Value { get; set; } = null!;
        }

        [Export("Common", typeof(object))]
        [ExportMetadata("Name", "First")]
        [ExportMetadata("Number", "Odd")]
        public class ExportFirst { }

        [Export("Common", typeof(object))]
        [ExportMetadata("Name", "Second")]
        [ExportMetadata("Number", "Even")]
        public class ExportSecond { }

        [Export("Common", typeof(object))]
        [ExportMetadata("Name", "Third")]
        [ExportMetadata("Number", "Odd")]
        public class ExportThird { }
    }
}
