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

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValuesEmpty(IContainer container)
        {
            IEnumerable<ICustomFormatter> results = container.GetExportedValues<ICustomFormatter>();
            Assert.Equal(0, results.Count());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValues(IContainer container)
        {
            IEnumerable<IExtension> results = container.GetExportedValues<IExtension>();
            Assert.Equal(2, results.Count());
            Assert.Equal(1, results.OfType<ExtensionOne>().Count());
            Assert.Equal(1, results.OfType<ExtensionTwo>().Count());
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValuesNamedEmpty(IContainer container)
        {
            IEnumerable<IExtension> results = container.GetExportedValues<IExtension>("BadName");
            Assert.Equal(0, results.Count());
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
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

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ImportsMethodsV3), typeof(ExportMembers))]
        public void ImportManyMethodsV3(IContainer container)
        {
            var importsMethodsExport = container.GetExportedValue<ImportsMethodsV3>();
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

            Assert.Equal(2, importsMethodsExport.ILazilyImportedMethods.Length);
            foreach (ILazy<Action> action in importsMethodsExport.ILazilyImportedMethods)
            {
                action.Value();
            }
        }

        [MefV1.Export]
        public class ImportsMethods
        {
            [MefV1.ImportMany]
            public Action[] ImportedMethods { get; set; }

            [MefV1.ImportMany]
            public Lazy<Action>[] LazilyImportedMethods { get; set; }
        }

        [MefV1.Export]
        public class ImportsMethodsV3 : ImportsMethods
        {
            [MefV1.ImportMany]
            public ILazy<Action>[] ILazilyImportedMethods { get; set; }
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
            var export =container.GetExportedValue<ExtendableListOfLazy>();
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
            public List<ILazy<IExtension>> Extensions { get; set; }
        }

        #endregion

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        [Trait("Container.GetExport", "CardinalityMismatch")]
        public void GetExportOneForManyThrowsException(IContainer container)
        {
            try
            {
                var result = container.GetExport<IExtension>();

                // MEFv1 would have thrown already, but MEFv2 needs a bit more help.
                var dummy = result.Value;

                Assert.False(true, "Expected exception not thrown.");
            }
            catch (InvalidOperationException)
            {
                // MEFv1 throws this.
            }
            catch (System.Composition.Hosting.CompositionFailedException)
            {
                // MEFv2 throws this
            }
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        [Trait("Container.GetExport", "CardinalityMismatch")]
        public void GetExportedValueOneForManyThrowsException(IContainer container)
        {
            try
            {
                container.GetExportedValue<IExtension>();
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (InvalidOperationException)
            {
                // MEFv1 throws this.
            }
            catch (System.Composition.Hosting.CompositionFailedException)
            {
                // MEFv2 throws this
            }
        }

        public interface IExtension { }

        [Export(typeof(IExtension))]
        [MefV1.Export(typeof(IExtension)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
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
            public IEnumerable<IExtension> Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableIList
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IList<IExtension> Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableList
        {
            [ImportMany]
            [MefV1.ImportMany]
            public List<IExtension> Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableArray
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IExtension[] Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableHashSet
        {
            [ImportMany]
            [MefV1.ImportMany]
            public HashSet<IExtension> Extensions { get; set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ExtendableCustomCollectionWithPublicCtor
        {
            [ImportMany]
            [MefV1.ImportMany]
            public CustomCollectionWithPublicCtor<IExtension> Extensions { get; set; }
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
            public CustomCollectionWithPublicCtor<IExtension> Extensions { get; set; }
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
            public CustomCollectionWithInternalCtor<IExtension> Extensions { get; set; }
        }

        public class CustomCollectionWithPublicCtor<T> : ICollection<T>
        {
            private List<T> inner = new List<T>();

            /// <summary>
            /// An internal constructor, to suppress the public one.
            /// </summary>
            public CustomCollectionWithPublicCtor()
            {
            }

            public CustomCollectionWithPublicCtor(object arg)
            {
                this.ConstructorArg = arg;
            }

            public object ConstructorArg { get; private set; }

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

        public class CustomCollectionWithInternalCtor<T> : CustomCollectionWithPublicCtor<T>
        {
            internal CustomCollectionWithInternalCtor()
            {
            }
        }
    }
}
