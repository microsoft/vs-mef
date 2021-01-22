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
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;
    using MefV1 = System.ComponentModel.Composition;

    public class ImportManyTests
    {
        #region Array tests

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableArray))]
        public void ImportManyArrayWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableArray>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Length);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableArray), typeof(ExtensionOne))]
        public void ImportManyArrayWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableArray>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Length);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableArray), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyArrayWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableArray>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Length);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableArrayWithMetadata), typeof(ExtensionOne))]
        public void ImportManyArrayWithOneAndMetadata(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableArrayWithMetadata>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Length);
            var value = extendable.Extensions.Single();
            Assert.IsAssignableFrom(typeof(ExtensionOne), value.Value);
            Assert.Equal(1, value.Metadata["a"]);
        }

        #endregion

        #region IEnumerable<T> tests

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableIEnumerable))]
        public void ImportManyIEnumerableWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIEnumerable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableIEnumerable), typeof(ExtensionOne))]
        public void ImportManyIEnumerableWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIEnumerable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count());
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtendableIEnumerable), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyIEnumerableWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIEnumerable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region IList<T> tests

        [MefFact(CompositionEngines.V2Compat, typeof(ExtendableIList))]
        public void ImportManyIListWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(ExtendableIList), typeof(ExtensionOne))]
        public void ImportManyIListWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V2Compat, typeof(ExtendableIList), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyIListWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableIList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region List<T> tests

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableList))]
        public void ImportManyListWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableList), typeof(ExtensionOne))]
        public void ImportManyListWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableList), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyListWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableList>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region Custom collection with public constructor tests

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionWithPublicCtor))]
        public void ImportManyCustomCollectionWithPublicCtorWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionWithPublicCtor>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionWithPublicCtor), typeof(ExtensionOne))]
        public void ImportManyCustomCollectionWithPublicCtorWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionWithPublicCtor>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionWithPublicCtor), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyCustomCollectionWithPublicCtorWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionWithPublicCtor>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionOfLazyMetadata), typeof(ExtensionOne))]
        public void ImportManyCustomCollectionWithLazyMetadata(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionOfLazyMetadata>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            var extension = extendable.Extensions.Single();
            Assert.IsType<ExtensionOne>(extension.Value);
            Assert.Equal(1, extension.Metadata["a"]);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionOfConcreteType), typeof(ExtensionOne))]
        public void ImportManyCustomCollectionConcreteType(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionOfConcreteType>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            var extension = extendable.Extensions.Single();
            Assert.IsType<ExtensionOne>(extension);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionOfConcreteTypeWithMetadata), typeof(ExtensionOne))]
        public void ImportManyCustomCollectionConcreteTypeWithMetadata(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionOfConcreteTypeWithMetadata>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            var extension = extendable.Extensions.Single();
            Assert.IsType<ExtensionOne>(extension.Value);
            Assert.Equal(1, extension.Metadata["a"]);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionWithPreInitializedPublicCtor), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyCustomCollectionWithPreInitializedPublicCtor(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionWithPreInitializedPublicCtor>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);

            Assert.NotNull(extendable.Extensions.ConstructorArg); // non-null indicates MEF didn't recreate the collection.
            Assert.True(extendable.Extensions.Cleared); // true indicates MEF did call Clear() before initializing.
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region Custom collection with internal constructor tests

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionWithInternalCtor))]
        public void ImportManyCustomCollectionWithInternalCtorWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionWithInternalCtor>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionWithInternalCtor), typeof(ExtensionOne))]
        public void ImportManyCustomCollectionWithInternalCtorWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionWithInternalCtor>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableCustomCollectionWithInternalCtor), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyCustomCollectionWithInternalCtorWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableCustomCollectionWithInternalCtor>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region HashSet<T> tests

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableHashSet))]
        public void ImportManyHashSetWithNone(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableHashSet>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableHashSet), typeof(ExtensionOne))]
        public void ImportManyHashSetWithOne(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableHashSet>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExtendableHashSet), typeof(ExtensionOne), typeof(ExtensionTwo))]
        public void ImportManyHashSetWithTwo(IContainer container)
        {
            var extendable = container.GetExportedValue<ExtendableHashSet>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        #endregion

        #region GetExportedValues tests

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtensionOne), typeof(ExtensionTwo))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValuesEmpty(IContainer container)
        {
            IEnumerable<ICustomFormatter> results = container.GetExportedValues<ICustomFormatter>();
            Assert.Equal(0, results.Count());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtensionOne), typeof(ExtensionTwo))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValues(IContainer container)
        {
            IEnumerable<IExtension> results = container.GetExportedValues<IExtension>();
            Assert.Equal(2, results.Count());
            Assert.Equal(1, results.OfType<ExtensionOne>().Count());
            Assert.Equal(1, results.OfType<ExtensionTwo>().Count());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtensionOne), typeof(ExtensionTwo))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValuesNamedEmpty(IContainer container)
        {
            IEnumerable<IExtension> results = container.GetExportedValues<IExtension>("BadName");
            Assert.Equal(0, results.Count());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(NamedExtensionOne), typeof(NamedExtensionTwo))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValuesNamed(IContainer container)
        {
            IEnumerable<IExtension> results = container.GetExportedValues<IExtension>("Named");
            Assert.Equal(2, results.Count());
            Assert.Equal(1, results.OfType<NamedExtensionOne>().Count());
            Assert.Equal(1, results.OfType<NamedExtensionTwo>().Count());
        }

        [Export("Named", typeof(IExtension))]
        [MefV1.Export("Named", typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NamedExtensionOne : IExtension { }

        [Export("Named", typeof(IExtension))]
        [MefV1.Export("Named", typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NamedExtensionTwo : IExtension { }

        #endregion

        #region ImportManyMethods test

        [MefFact(CompositionEngines.V1Compat, typeof(ImportsMethods), typeof(ExportMembers))]
        public void ImportManyMethods(IContainer container)
        {
            var importsMethodsExport = container.GetExportedValue<ImportsMethods>();

            Assert.Equal(2, importsMethodsExport.ImportedMethods.Length);
            foreach (Action action in importsMethodsExport.ImportedMethods)
            {
                action();
            }

            Assert.Equal(2, importsMethodsExport.LazilyImportedMethods.Length);
            foreach (Lazy<Action> action in importsMethodsExport.LazilyImportedMethods)
            {
                action.Value();
            }
        }

        [MefV1.Export]
        public class ImportsMethods
        {
            [MefV1.ImportMany]
            public Action[] ImportedMethods { get; set; } = null!;

            [MefV1.ImportMany]
            public Lazy<Action>[] LazilyImportedMethods { get; set; } = null!;
        }

        public class ExportMembers
        {
            [MefV1.Export]
            public void ActionMethod1()
            {
            }

            [MefV1.Export]
            public void ActionMethod2()
            {
            }
        }

        #endregion

        #region NonPublic ImportMany tests

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(NonPublicExtensionThree), typeof(ExtendableListOfLazy))]
        public void ImportManyListOfLazyPublicInterfaceWithNonPublicParts(IContainer container)
        {
            var export = container.GetExportedValue<ExtendableListOfLazy>();
            Assert.Equal(1, export.Extensions.Count);
            Assert.IsType<NonPublicExtensionThree>(export.Extensions.Single().Value);
        }

        [Export("NonPublic", typeof(IExtension))]
        [MefV1.Export("NonPublic", typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        internal class NonPublicExtensionThree : IExtension { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableListOfLazy
        {
            [ImportMany("NonPublic")]
            [MefV1.ImportMany("NonPublic")]
            public List<Lazy<IExtension>> Extensions { get; set; } = null!;
        }

        #endregion

        #region Import of non-public export using public custom collection

        [MefFact(CompositionEngines.V1Compat, typeof(PartThatImportsNonPublicTypeWithPublicCustomCollection), typeof(InternalExtension1))]
        public void ImportManyNonPublicUsingPublicCustomCollection(IContainer container)
        {
            var importer = container.GetExport<PartThatImportsNonPublicTypeWithPublicCustomCollection>();
            Assert.IsType<InternalExtension1>(importer.Value.ImportingCollection.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartThatImportsNonPublicTypeWithPublicCustomCollectionAndMetadataView), typeof(InternalExtension1))]
        public void ImportManyNonPublicUsingPublicCustomCollectionWithMetadataView(IContainer container)
        {
            var importer = container.GetExport<PartThatImportsNonPublicTypeWithPublicCustomCollectionAndMetadataView>();
            Assert.IsType<InternalExtension1>(importer.Value.ImportingCollection.Single().Value);
        }

        internal interface IInternalExtension { }

        [Export(typeof(IInternalExtension))]
        [MefV1.Export(typeof(IInternalExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        internal class InternalExtension1 : IInternalExtension { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PartThatImportsNonPublicTypeWithPublicCustomCollection
        {
            [ImportMany]
            [MefV1.ImportMany]
            internal CustomCollectionWithPublicCtor<IInternalExtension> ImportingCollection { get; set; } = null!;
        }

        public interface IPublicMetadataView { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PartThatImportsNonPublicTypeWithPublicCustomCollectionAndMetadataView
        {
            [ImportMany]
            [MefV1.ImportMany]
            internal CustomCollectionWithPublicCtor<IInternalExtension, IPublicMetadataView> ImportingCollection { get; set; } = null!;
        }

        #endregion

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtensionOne), typeof(ExtensionTwo))]
        [Trait("Container.GetExport", "CardinalityMismatch")]
        public void GetExportOneForManyThrowsException(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() =>
            {
                var result = container.GetExport<IExtension>();

                // MEFv1 would have thrown already, but MEFv2 needs a bit more help.
                var dummy = result.Value;
            });
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExtensionOne), typeof(ExtensionTwo))]
        [Trait("Container.GetExport", "CardinalityMismatch")]
        public void GetExportedValueOneForManyThrowsException(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<IExtension>());
        }

        public interface IExtension { }

        [Export(typeof(IExtension)), ExportMetadata("a", 1)]
        [MefV1.Export(typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared), MefV1.ExportMetadata("a", 1)]
        public class ExtensionOne : IExtension { }

        [Export(typeof(IExtension))]
        [MefV1.Export(typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtensionTwo : IExtension { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableIEnumerable
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IEnumerable<IExtension> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableIList
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IList<IExtension> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableList
        {
            [ImportMany]
            [MefV1.ImportMany]
            public List<IExtension> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableArray
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IExtension[] Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableArrayWithMetadata
        {
            [ImportMany]
            [MefV1.ImportMany]
            public Lazy<IExtension, IDictionary<string, object>>[] Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableHashSet
        {
            [ImportMany]
            [MefV1.ImportMany]
            public HashSet<IExtension> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableCustomCollectionWithPublicCtor
        {
            [ImportMany]
            [MefV1.ImportMany]
            public CustomCollectionWithPublicCtor<IExtension> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableCustomCollectionWithPreInitializedPublicCtor
        {
            public ExtendableCustomCollectionWithPreInitializedPublicCtor()
            {
                this.Extensions = new CustomCollectionWithPublicCtor<IExtension>(new object());
            }

            [ImportMany]
            [MefV1.ImportMany]
            public CustomCollectionWithPublicCtor<IExtension> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableCustomCollectionWithInternalCtor
        {
            public ExtendableCustomCollectionWithInternalCtor()
            {
                this.Extensions = new CustomCollectionWithInternalCtor<IExtension>();
            }

            [ImportMany]
            [MefV1.ImportMany]
            public CustomCollectionWithInternalCtor<IExtension> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableCustomCollectionOfLazyMetadata
        {
            [ImportMany]
            [MefV1.ImportMany]
            public CustomCollectionWithLazyMetadata<IExtension, IDictionary<string, object>> Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableCustomCollectionOfConcreteType
        {
            [ImportMany]
            [MefV1.ImportMany]
            public CustomCollectionConcreteType Extensions { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableCustomCollectionOfConcreteTypeWithMetadata
        {
            [ImportMany]
            [MefV1.ImportMany]
            public CustomCollectionConcreteTypeWithMetadata Extensions { get; set; } = null!;
        }

        public class CustomCollectionWithPublicCtor<T> : ICollection<T>
        {
            // We have this here just to ensure that VS MEF doesn't mess up when it's present.
            static CustomCollectionWithPublicCtor() { }

            private List<T> inner = new List<T>();

            /// <summary>
            /// Initializes a new instance of the <see cref="CustomCollectionWithPublicCtor{T}"/> class.
            /// </summary>
            public CustomCollectionWithPublicCtor()
            {
            }

            public CustomCollectionWithPublicCtor(object? arg)
            {
                this.ConstructorArg = arg;
            }

            public object? ConstructorArg { get; private set; }

            public bool Cleared { get; set; }

            public void Add(T item)
            {
                this.inner.Add(item);
            }

            public void Clear()
            {
                this.inner.Clear();
                this.Cleared = true;
            }

            public bool Contains(T item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { return this.inner.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public bool Remove(T item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<T> GetEnumerator()
            {
                return this.inner.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        public class CustomCollectionWithPublicCtor<T, TMetadata> : CustomCollectionWithPublicCtor<Lazy<T, TMetadata>> { }

        public class CustomCollectionWithInternalCtor<T> : CustomCollectionWithPublicCtor<T>
        {
            internal CustomCollectionWithInternalCtor()
            {
            }
        }

        public class CustomCollectionWithLazyMetadata<T, TMetadata> : ICollection<Lazy<T, TMetadata>>
        {
            private List<Lazy<T, TMetadata>> inner = new List<Lazy<T, TMetadata>>();

            /// <summary>
            /// Initializes a new instance of the <see cref="CustomCollectionWithLazyMetadata{T, TMetadata}"/> class.
            /// </summary>
            public CustomCollectionWithLazyMetadata()
            {
            }

            public bool Cleared { get; set; }

            public void Add(Lazy<T, TMetadata> item)
            {
                this.inner.Add(item);
            }

            public void Clear()
            {
                this.inner.Clear();
                this.Cleared = true;
            }

            public bool Contains(Lazy<T, TMetadata> item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(Lazy<T, TMetadata>[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { return this.inner.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public bool Remove(Lazy<T, TMetadata> item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<Lazy<T, TMetadata>> GetEnumerator()
            {
                return this.inner.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        public class CustomCollectionConcreteType : CustomCollectionWithPublicCtor<IExtension> { }

        public class CustomCollectionConcreteTypeWithMetadata : CustomCollectionWithPublicCtor<Lazy<IExtension, IDictionary<string, object>>> { }
    }
}
