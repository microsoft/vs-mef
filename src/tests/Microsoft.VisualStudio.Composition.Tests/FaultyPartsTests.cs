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
    using Xunit.Abstractions;
    using MefV1 = System.ComponentModel.Composition;

    /// <summary>
    /// Tests MEF behaviors surrounding parts that throw exceptions at various stages of initialization.
    /// </summary>
    public class FaultyPartsTests
    {
        private const string CustomContractName = "SomeContractName";

        public FaultyPartsTests(ITestOutputHelper logger)
        {
            this.Logger = logger;
        }

        public ITestOutputHelper Logger { get; }

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

        [Fact]
        public async Task MissingExportErrorMessageDescribesImportIdentity()
        {
            var configuration = await TestUtilities.CreateConfigurationAsync(CompositionEngines.V1, typeof(PartThatImportsMissingExportWithSpecialTypeIdentity));
            var level1 = configuration.CompositionErrors.Peek();
            var error = level1.Single();
            this.Logger.WriteLine(error.Message);
            Assert.Contains(typeof(IServiceProvider).FullName, error.Message);
        }

        [Fact]
        public async Task MissingExportErrorMessageDescribesImportIdentityAndContractName()
        {
            var configuration = await TestUtilities.CreateConfigurationAsync(CompositionEngines.V1, typeof(PartThatImportsMissingExportWithSpecialTypeIdentityAndContractName));
            var level1 = configuration.CompositionErrors.Peek();
            var error = level1.Single();
            this.Logger.WriteLine(error.Message);
            Assert.Contains(typeof(IServiceProvider).FullName, error.Message);
            Assert.Contains(CustomContractName, error.Message);
        }

        [Fact]
        public async Task TooManyExportsErrorMessageDescribesImportIdentity()
        {
            var configuration = await TestUtilities.CreateConfigurationAsync(
                CompositionEngines.V1,
                typeof(PartThatConditionallyImportsMissingExportWithSpecialTypeIdentity),
                typeof(MultipleServiceProviderExports));
            var level1 = configuration.CompositionErrors.Peek();
            var error = level1.Single();
            this.Logger.WriteLine(error.Message);
            Assert.Contains(typeof(IServiceProvider).FullName, error.Message);
        }

        [Fact]
        public async Task TooManyExportsErrorMessageDescribesImportIdentityAndContractName()
        {
            var configuration = await TestUtilities.CreateConfigurationAsync(
                CompositionEngines.V1,
                typeof(PartThatConditionallyImportsMissingExportWithSpecialTypeIdentityAndContractName),
                typeof(MultipleServiceProviderExportsWithCustomContractName));
            var level1 = configuration.CompositionErrors.Peek();
            var error = level1.Single();
            this.Logger.WriteLine(error.Message);
            Assert.Contains(typeof(IServiceProvider).FullName, error.Message);
            Assert.Contains(CustomContractName, error.Message);
        }

        [Fact]
        public async Task ImportManyOnNonCollectionMember()
        {
            var composition = await TestUtilities.CreateConfigurationAsync(CompositionEngines.V1, typeof(PartWithImportManyOnNonCollection));
            var error = composition.Catalog.DiscoveredParts.DiscoveryErrors.Single();
            this.Logger.WriteLine(error.ToString());
            var rootCause = TestUtilities.GetInnermostException(error);
            Assert.Contains(typeof(IServiceProvider).FullName, rootCause.Message);
            Assert.Contains(typeof(ImportManyAttribute).Name, rootCause.Message);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportManyOnNonCollection
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IServiceProvider SP { get; set; } = null!;
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
            public OrdinaryPart? ImportingProperty
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
            public Lazy<ImportingConstructorThrowsPart> Import { get; set; } = null!;
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

        [MefV1.Export]
        public class PartThatImportsMissingExportWithSpecialTypeIdentity
        {
            [MefV1.ImportingConstructor]
            public PartThatImportsMissingExportWithSpecialTypeIdentity([MefV1.Import(typeof(IServiceProvider))] IFormatProvider projectGuidService)
            {
            }
        }

        [MefV1.Export]
        public class PartThatImportsMissingExportWithSpecialTypeIdentityAndContractName
        {
            [MefV1.ImportingConstructor]
            public PartThatImportsMissingExportWithSpecialTypeIdentityAndContractName([MefV1.Import(CustomContractName, typeof(IServiceProvider))] IFormatProvider projectGuidService)
            {
            }
        }

        [MefV1.Export]
        public class PartThatConditionallyImportsMissingExportWithSpecialTypeIdentity
        {
            [MefV1.ImportingConstructor]
            public PartThatConditionallyImportsMissingExportWithSpecialTypeIdentity([MefV1.Import(typeof(IServiceProvider), AllowDefault = true)] IFormatProvider projectGuidService)
            {
            }
        }

        [MefV1.Export]
        public class PartThatConditionallyImportsMissingExportWithSpecialTypeIdentityAndContractName
        {
            [MefV1.ImportingConstructor]
            public PartThatConditionallyImportsMissingExportWithSpecialTypeIdentityAndContractName([MefV1.Import(CustomContractName, typeof(IServiceProvider), AllowDefault = true)] IFormatProvider projectGuidService)
            {
            }
        }

        public class MultipleServiceProviderExports
        {
            [MefV1.Export(typeof(IServiceProvider))]
            public IFormatProvider? ExportingProperty { get; }

            [MefV1.Export(typeof(IServiceProvider))]
            public IFormatProvider? ExportingProperty2 { get; }
        }

        public class MultipleServiceProviderExportsWithCustomContractName
        {
            [MefV1.Export(CustomContractName, typeof(IServiceProvider))]
            public IFormatProvider? ExportingProperty { get; }

            [MefV1.Export(CustomContractName, typeof(IServiceProvider))]
            public IFormatProvider? ExportingProperty2 { get; }
        }

        [MefFact(CompositionEngines.V1Compat, typeof(CustomCollectionUninitializedPartViaParameter), InvalidConfiguration = true)]
        public void UninitializedCustomCollectionViaParameter(IContainer container)
        {
            container.GetExportedValue<CustomCollectionUninitializedPartViaParameter>();
        }

        [MefFact(CompositionEngines.V1Compat, typeof(CustomCollectionUninitializedPartViaProperty))]
        public void UninitializedCustomCollectionViaProperty(IContainer container)
        {
            var ex = Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<CustomCollectionUninitializedPartViaProperty>());
            this.Logger.WriteLine(ex.ToString());
            Assert.Contains(nameof(CustomCollectionUninitializedPartViaProperty.Exports), ex.Message);
            Assert.Contains(nameof(CustomCollection<int>), ex.Message);
        }

        [MefV1.Export]
        public class CustomCollectionUninitializedPartViaParameter
        {
            [MefV1.ImportingConstructor]
            public CustomCollectionUninitializedPartViaParameter([MefV1.ImportMany] CustomCollection<IServiceProvider> exports)
            {
            }
        }

        [MefV1.Export]
        public class CustomCollectionUninitializedPartViaProperty
        {
            [MefV1.ImportMany]
            public CustomCollection<IServiceProvider> Exports { get; set; } = null!;
        }

        public class CustomCollection<T> : List<T>
        {
            public CustomCollection(IFormatProvider specialArgument) { }
        }

        private static void AssertPartThrowsV1<T>(IContainer container)
        {
            AssertThrowsV1(() => container.GetExportedValue<T>());
        }

        private static void AssertThrowsV1(Func<object?> action)
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

        private static bool IsSomeInnerException<T>(Exception? ex)
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
                    foreach (var error in mefv1Exception.Errors)
                    {
                        if (IsSomeInnerException<T>(error.Exception))
                        {
                            return true;
                        }
                    }
                }

                ex = ex.InnerException;
            }

            return false;
        }

        private class MyException : Exception { }
    }
}
