// Copyright (c) Microsoft. All rights reserved.

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

    /// <summary>
    /// Tests MEF behaviors surrounding parts that throw exceptions at various stages of initialization.
    /// </summary>
    public class FaultyPartsTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(ImportingConstructorThrowsPart))]
        public void GetExportedValue_ImportingConstructorThrowsV1(IContainer container)
        {
            AssertPartThrowsV1<ImportingConstructorThrowsPart>(container);
        }

        [MefFact(CompositionEngines.V2, typeof(ImportingConstructorThrowsPart), NoCompatGoal = true)]
        public void GetExportedValue_ImportingConstructorThrowsV2(IContainer container)
        {
            Assert.Throws<MyException>(() => container.GetExportedValue<ImportingConstructorThrowsPart>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(ImportingConstructorThrowsPart), typeof(PartImportsPartThatThrowsViaImportingConstructor))]
        public void GetExportedValue_PartImportingPartThatThrowsViaImportingConstructorV1(IContainer container)
        {
            AssertPartThrowsV1<PartImportsPartThatThrowsViaImportingConstructor>(container);
        }

        [MefFact(CompositionEngines.V2, typeof(ImportingConstructorThrowsPart), typeof(PartImportsPartThatThrowsViaImportingConstructor), NoCompatGoal = true)]
        public void GetExportedValue_PartImportingPartThatThrowsViaImportingConstructorV2(IContainer container)
        {
            Assert.Throws<MyException>(() => container.GetExportedValue<PartImportsPartThatThrowsViaImportingConstructor>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(ImportingPropertySetterThrowsPart), typeof(OrdinaryPart))]
        public void GetExportedValue_ImportingPropertySetterThrowsV1(IContainer container)
        {
            AssertPartThrowsV1<ImportingPropertySetterThrowsPart>(container);
        }

        [MefFact(CompositionEngines.V2, typeof(ImportingPropertySetterThrowsPart), typeof(OrdinaryPart), NoCompatGoal = true)]
        public void GetExportedValue_ImportingPropertySetterThrowsV2(IContainer container)
        {
            Assert.Throws<MyException>(() => container.GetExportedValue<ImportingPropertySetterThrowsPart>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartThrowsInOnImportsSatisfied))]
        public void GetExportedValue_OnImportsSatisfiedThrowsV1(IContainer container)
        {
            AssertPartThrowsV1<PartThrowsInOnImportsSatisfied>(container);
        }

        [MefFact(CompositionEngines.V2, typeof(PartThrowsInOnImportsSatisfied), NoCompatGoal = true)]
        public void GetExportedValue_OnImportsSatisfiedThrowsV2(IContainer container)
        {
            Assert.Throws<MyException>(() => container.GetExportedValue<PartThrowsInOnImportsSatisfied>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartImportsPartThatThrowsViaLazyProperty), typeof(ImportingConstructorThrowsPart))]
        public void LazyImportingProperty_ImportingConstructorThrowsV1(IContainer container)
        {
            var lazyImporter = container.GetExportedValue<PartImportsPartThatThrowsViaLazyProperty>();
            AssertThrowsV1(() => lazyImporter.Import.Value);
        }

        [MefFact(CompositionEngines.V2, typeof(PartImportsPartThatThrowsViaLazyProperty), typeof(ImportingConstructorThrowsPart), NoCompatGoal = true)]
        public void LazyImportingProperty_ImportingConstructorThrowsV2(IContainer container)
        {
            var lazyImporter = container.GetExportedValue<PartImportsPartThatThrowsViaLazyProperty>();
            Assert.Throws<MyException>(() => lazyImporter.Import.Value);
        }

        [Export, Shared]
        [MefV1.Export]
        public class ImportingConstructorThrowsPart
        {
            public ImportingConstructorThrowsPart()
            {
                throw new MyException();
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class ImportingPropertySetterThrowsPart
        {
            [Import, MefV1.Import]
            public OrdinaryPart ImportingProperty
            {
                get { return null; }
                set { throw new MyException(); }
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartImportsPartThatThrowsViaImportingConstructor
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartImportsPartThatThrowsViaImportingConstructor(ImportingConstructorThrowsPart part) { }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartImportsPartThatThrowsViaLazyProperty
        {
            [Import, MefV1.Import]
            public Lazy<ImportingConstructorThrowsPart> Import { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThrowsInOnImportsSatisfied : MefV1.IPartImportsSatisfiedNotification
        {
            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                throw new MyException();
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class OrdinaryPart { }

        private static void AssertPartThrowsV1<T>(IContainer container)
        {
            AssertThrowsV1(() => container.GetExportedValue<T>());
        }

        private static void AssertThrowsV1(Func<object> action)
        {
            try
            {
                action();
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (CompositionFailedException ex)
            {
                Assert.True(IsSomeInnerException<MyException>(ex));
            }
            catch (MefV1.CompositionException ex)
            {
                // Lazy<T> can throw this and our test wrapper container won't be able to convert the type.
                Assert.True(IsSomeInnerException<MyException>(ex));
            }
        }

        private static bool IsSomeInnerException<T>(Exception ex)
        {
            while (ex != null)
            {
                if (ex is T)
                {
                    return true;
                }

                var mefv1Exception = ex as MefV1.CompositionException;
                if (mefv1Exception != null)
                {
#if DESKTOP
                    foreach (var error in mefv1Exception.Errors)
                    {
                        if (IsSomeInnerException<T>(error.Exception))
                        {
                            return true;
                        }
                    }
#else
                    throw new NotSupportedException();
#endif
                }

                ex = ex.InnerException;
            }

            return false;
        }

        private class MyException : Exception { }
    }
}
