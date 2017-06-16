// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Shell.Interop;
    using Xunit;
    using Xunit.Abstractions;
    using MEFv1 = System.ComponentModel.Composition;

    public class CompositionCatalogTests
    {
        private readonly ITestOutputHelper logger;

        public CompositionCatalogTests(ITestOutputHelper logger)
        {
            this.logger = logger;
        }

        public static PartDiscovery[] DiscoveryEngines
        {
            get
            {
                return new PartDiscovery[]
                {
#if DESKTOP
                    TestUtilities.V1Discovery,
#endif
                    TestUtilities.V2Discovery,
                };
            }
        }

        public static object[][] DiscoveryEnginesTheoryData
        {
            get
            {
                return DiscoveryEngines.Select(e => new object[] { e }).ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task CreateFromTypesOmitsNonParts(PartDiscovery discovery)
        {
            var catalog = ComposableCatalog.Create(discovery.Resolver).AddParts(
                await discovery.CreatePartsAsync(typeof(NonExportingType), typeof(ExportingType)));
            Assert.Equal(1, catalog.Parts.Count);
            Assert.Equal(typeof(ExportingType), catalog.Parts.Single().Type);
        }

        [Fact]
        public void AddPartNullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => TestUtilities.EmptyCatalog.AddPart(null));
        }

        [Fact]
        public void GetAssemblyInputs_Empty()
        {
            Assert.Equal(0, TestUtilities.EmptyCatalog.GetInputAssemblies().Count);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningParts(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(NonExportingType), typeof(ExportingType)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(NonExportingType).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName(),
            };
            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningBaseTypesOfParts(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingTypeDerivesFromOtherAssembly)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(ExportingTypeDerivesFromOtherAssembly).GetTypeInfo().Assembly.GetName(),
                typeof(AssemblyDiscoveryTests.NonPart).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName(),
            };
            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningInterfacesOfParts(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingTypeImplementsFromOtherAssembly)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(ExportingTypeImplementsFromOtherAssembly).GetTypeInfo().Assembly.GetName(),
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName(),
            };
            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningEnumUsedInPartMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(PartWithEnumValueMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(PartWithEnumValueMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningTypeSingleMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingWithTypeSingleMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingWithTypeSingleMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningTypeMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingWithTypeMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingWithTypeMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningMultipleTypeMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingWithMultipleTypeMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AllColorableItemInfo).GetTypeInfo().Assembly.GetName(),
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingWithMultipleTypeMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningEnumMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingWithEnumMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.SomeEnum).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingWithEnumMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningMultipleDifferentEnumMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingWithMultipleDifferentEnumMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.SomeEnum).GetTypeInfo().Assembly.GetName(),
                typeof(AssemblyDiscoveryTests2.SomeOtherEnum).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingWithMultipleDifferentEnumMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningLotsOfMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingWithLotsOfMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.SomeEnum).GetTypeInfo().Assembly.GetName(),
                typeof(AssemblyDiscoveryTests2.SomeOtherEnum).GetTypeInfo().Assembly.GetName(),
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingWithLotsOfMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningExportingMembersWithTypeMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingWithExportingMembers)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingWithExportingMembers).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_FunctionsCorrectlyWithNullMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingTypeWithNullExportMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingTypeWithNullExportMetadata).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_RecursesThroughTypeTreeInMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingTypeWithExportMetadataWithExternalDependencies)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterface).GetTypeInfo().Assembly.GetName(),
                typeof(System.Exception).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingTypeWithExportMetadataWithExternalDependencies).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_RecursesThroughInterfaceTreeInMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingTypeWithExportMetadataWithExternalDependenciesAndInterfaceTree)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterfaceWithBaseInterface).GetTypeInfo().Assembly.GetName(),
                typeof(AssemblyDiscoveryTests2.IBlankInterface).GetTypeInfo().Assembly.GetName(),
                typeof(ExportingTypeWithExportMetadataWithExternalDependenciesAndInterfaceTree).GetTypeInfo().Assembly.GetName(),
                typeof(object).GetTypeInfo().Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            this.AssertExpectedInputAssemblies(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DiscoveryEnginesTheoryData))]
        public async Task GetAssemblyInputs_ContainsDefiningAttributeAssemblyForMetadata(PartDiscovery discovery)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await discovery.CreatePartsAsync(typeof(ExportingTypeWithMetadataWhoseDefiningAttributeIsInAnotherAssembly)));

            var inputAssemblies = catalog.GetInputAssemblies();
            Assert.Contains(typeof(SomeMetadataAttributeFromAnotherAssemblyAttribute).GetTypeInfo().Assembly.GetName(), inputAssemblies, AssemblyNameComparer.Default);
        }

        private void AssertExpectedInputAssemblies(ISet<AssemblyName> expectedSubset, IEnumerable<AssemblyName> actual)
        {
            this.logger.WriteLine("Expected:");
            this.logger.WriteLine(string.Join(Environment.NewLine, expectedSubset.OrderBy(async => async.FullName)));
            this.logger.WriteLine("Actual:");
            this.logger.WriteLine(string.Join(Environment.NewLine, actual.OrderBy(async => async.FullName)));
            Assert.True(expectedSubset.IsSubsetOf(actual)); // Allow for extra assemblies, since V2 discovery adds MS.VS.Composition itself for Shared/NonShared part metadata
        }

        public class NonExportingType { }

        [Export, MEFv1.Export]
        public class ExportingType { }

        [Export, MEFv1.Export]
        [SomeMetadataAttributeFromAnotherAssembly("My property value")]
        public class ExportingTypeWithMetadataWhoseDefiningAttributeIsInAnotherAssembly { }

        [Export, MEFv1.Export]
        [ExportMetadata("External", typeof(ClassWithExternalDependencies))]
        [MEFv1.ExportMetadata("External", typeof(ClassWithExternalDependencies))]
        public class ExportingTypeWithExportMetadataWithExternalDependencies
        {
        }

        [Export, MEFv1.Export]
        [ExportMetadata("External", typeof(ClassWithExternalDependenciesAndInterfaceTree))]
        [MEFv1.ExportMetadata("External", typeof(ClassWithExternalDependenciesAndInterfaceTree))]
        public class ExportingTypeWithExportMetadataWithExternalDependenciesAndInterfaceTree
        {
        }

        public class ClassWithExternalDependencies : System.Exception, AssemblyDiscoveryTests.ISomeInterface { }

        public class ClassWithExternalDependenciesAndInterfaceTree : AssemblyDiscoveryTests.ISomeInterfaceWithBaseInterface { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("Null", null)]
        [MultipleTypeMetadata(typeof(AssemblyDiscoveryTests.ISomeInterface))]
        [MultipleTypeMetadata(null)]
        public class ExportingTypeWithNullExportMetadata { }

        [Export, MEFv1.Export]
        public class ExportingTypeDerivesFromOtherAssembly : AssemblyDiscoveryTests.NonPart { }

        [Export, MEFv1.Export]
        public class ExportingTypeImplementsFromOtherAssembly : AssemblyDiscoveryTests.ISomeInterface { }

        [Export, MEFv1.Export]
        [PartMetadata("ExternalAssemblyValue", typeof(AssemblyDiscoveryTests.ISomeInterface))]
        [MEFv1.PartMetadata("ExternalAssemblyValue", typeof(AssemblyDiscoveryTests.ISomeInterface))]
        public class PartWithEnumValueMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("Type", typeof(AssemblyDiscoveryTests.ISomeInterface))]
        [ExportMetadata("Type", typeof(AssemblyDiscoveryTests.ISomeInterface))]
        public class ExportingWithTypeMetadata { }

        [Export, MEFv1.Export]
        [MultipleTypeMetadata(typeof(AssemblyDiscoveryTests.ISomeInterface))]
        [MultipleTypeMetadata(typeof(AllColorableItemInfo))]
        public class ExportingWithMultipleTypeMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("AdornmentLayerType", typeof(AssemblyDiscoveryTests.ISomeInterface), IsMultiple = false)]
        [ExportMetadata("AdornmentLayerType", typeof(AssemblyDiscoveryTests.ISomeInterface))]
        public class ExportingWithTypeSingleMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("Position", AssemblyDiscoveryTests.SomeEnum.SomeEnumValue)]
        [ExportMetadata("Position", AssemblyDiscoveryTests.SomeEnum.SomeEnumValue)]
        public class ExportingWithEnumMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("SomeEnum", AssemblyDiscoveryTests.SomeEnum.SomeEnumValue)]
        [MEFv1.ExportMetadata("SomeOtherEnum", AssemblyDiscoveryTests2.SomeOtherEnum.EnumValue)]
        [ExportMetadata("SomeEnum", AssemblyDiscoveryTests.SomeEnum.SomeEnumValue)]
        [ExportMetadata("SomeOtherEnum", AssemblyDiscoveryTests2.SomeOtherEnum.EnumValue)]
        public class ExportingWithMultipleDifferentEnumMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("SomeEnum", AssemblyDiscoveryTests.SomeEnum.SomeEnumValue)]
        [MEFv1.ExportMetadata("SomeOtherEnum", AssemblyDiscoveryTests2.SomeOtherEnum.EnumValue)]
        [MEFv1.ExportMetadata("SomeInterface", typeof(AssemblyDiscoveryTests.ISomeInterface))]
        [ExportMetadata("SomeEnum", AssemblyDiscoveryTests.SomeEnum.SomeEnumValue)]
        [ExportMetadata("SomeOtherEnum", AssemblyDiscoveryTests2.SomeOtherEnum.EnumValue)]
        [ExportMetadata("SomeInterface", typeof(AssemblyDiscoveryTests.ISomeInterface))]
        [MultipleTypeMetadata(typeof(AssemblyDiscoveryTests.SomeEnum))]
        [MultipleTypeMetadata(typeof(AssemblyDiscoveryTests2.SomeOtherEnum))]
        [PartMetadata("ExternalAssemblyValue", typeof(AssemblyDiscoveryTests.SomeEnum))]
        [MEFv1.PartMetadata("ExternalAssemblyValue", typeof(AssemblyDiscoveryTests.SomeEnum))]
        public class ExportingWithLotsOfMetadata { }

        public class ExportingWithExportingMembers
        {
            [Export, MEFv1.Export]
            [MEFv1.ExportMetadata("SomeInterface", typeof(AssemblyDiscoveryTests.ISomeInterface))]
            [ExportMetadata("SomeInterface", typeof(AssemblyDiscoveryTests.ISomeInterface))]
            public object Export { get; set; }
        }

        [MetadataAttribute, MEFv1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
        internal sealed class MultipleTypeMetadataAttribute : Attribute
        {
            public MultipleTypeMetadataAttribute(Type type)
            {
                this.Type = type;
            }

            public Type Type { get; private set; }
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            internal static readonly AssemblyNameComparer Default = new AssemblyNameComparer();

            internal AssemblyNameComparer() { }

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                if (x == null ^ y == null)
                {
                    return false;
                }

                if (x == null)
                {
                    return true;
                }

#if DESKTOP
                // fast path
                if (x.CodeBase == y.CodeBase)
                {
                    return true;
                }
#endif

                // Testing on FullName is horrifically slow.
                // So test directly on its components instead.
                return x.Name == y.Name
                    && x.Version.Equals(y.Version)
                    && x.CultureName.Equals(y.CultureName);
            }

            public int GetHashCode(AssemblyName obj)
            {
                return obj.Name.GetHashCode();
            }
        }
    }
}
