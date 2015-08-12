namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Language.Intellisense;
    using Shell.Interop;
    using Text.Editor;
    using Xunit;
    using MEFv1 = System.ComponentModel.Composition;

    public class CompositionCatalogTests
    {
        [Fact]
        public async Task CreateFromTypesOmitsNonPartsV1()
        {
            var discovery = TestUtilities.V1Discovery;
            var catalog = ComposableCatalog.Create(discovery.Resolver).AddParts(
                await discovery.CreatePartsAsync(typeof(NonExportingType), typeof(ExportingType)));
            Assert.Equal(1, catalog.Parts.Count);
            Assert.Equal(typeof(ExportingType), catalog.Parts.Single().Type);
        }

        [Fact]
        public async Task CreateFromTypesOmitsNonPartsV2()
        {
            var discovery = TestUtilities.V2Discovery;
            var catalog = TestUtilities.EmptyCatalog.AddParts(
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

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningParts()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(NonExportingType), typeof(ExportingType)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(NonExportingType).Assembly.GetName(),
                typeof(object).Assembly.GetName(),
            };
            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningBaseTypesOfParts()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingTypeDerivesFromOtherAssembly)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(ExportingTypeDerivesFromOtherAssembly).Assembly.GetName(),
                typeof(AssemblyDiscoveryTests.NonPart).Assembly.GetName(),
                typeof(object).Assembly.GetName(),
            };
            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningInterfacesOfParts()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingTypeImplementsFromOtherAssembly)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(ExportingTypeImplementsFromOtherAssembly).Assembly.GetName(),
                typeof(AssemblyDiscoveryTests.ISomeInterface).Assembly.GetName(),
                typeof(object).Assembly.GetName(),
            };
            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningEnumUsedInPartMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(PartWithEnumValueMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(PartWithEnumValueMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningTypeSingleMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithTypeSingleMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingWithTypeSingleMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningTypeMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithTypeMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingWithTypeMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningMultipleTypeMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithMultipleTypeMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AllColorableItemInfo).Assembly.GetName(),
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingWithMultipleTypeMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningEnumMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithEnumMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(ExportingWithEnumMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningMultipleDifferentEnumMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithMultipleDifferentEnumMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(SmartTagState).Assembly.GetName(),
                typeof(ExportingWithMultipleDifferentEnumMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningLotsOfMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithLotsOfMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(AllColorableItemInfo).Assembly.GetName(),
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(SmartTagState).Assembly.GetName(),
                typeof(ExportingWithLotsOfMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesDefiningExportingMembersWithTypeMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithExportingMembers)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingWithExportingMembers).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_FunctionsCorrectlyWithNullMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingTypeWithNullExportMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingTypeWithNullExportMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_RecursesThroughTypeTreeInMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingTypeWithExportMetadataWithExternalDependencies)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterface).Assembly.GetName(),
                typeof(CompletionPresenterStyle).Assembly.GetName(),
                typeof(ExportingTypeWithExportMetadataWithExternalDependencies).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_RecursesThroughInterfaceTreeInMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingTypeWithExportMetadataWithExternalDependenciesAndInterfaceTree)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AssemblyDiscoveryTests.ISomeInterfaceWithBaseInterface).Assembly.GetName(),
                typeof(AssemblyDiscoveryTests2.IBlankInterface).Assembly.GetName(),
                typeof(ExportingTypeWithExportMetadataWithExternalDependenciesAndInterfaceTree).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        public class NonExportingType { }

        [Export, MEFv1.Export]
        public class ExportingType { }

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

        public class ClassWithExternalDependencies : CompletionPresenterStyle, AssemblyDiscoveryTests.ISomeInterface { }

        public class ClassWithExternalDependenciesAndInterfaceTree : AssemblyDiscoveryTests.ISomeInterfaceWithBaseInterface { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("Null", null)]
        [MultipleTypeMetadata(typeof(IAdornmentLayer))]
        [MultipleTypeMetadata(null)]
        public class ExportingTypeWithNullExportMetadata { }

        [Export, MEFv1.Export]
        public class ExportingTypeDerivesFromOtherAssembly : AssemblyDiscoveryTests.NonPart { }

        [Export, MEFv1.Export]
        public class ExportingTypeImplementsFromOtherAssembly : AssemblyDiscoveryTests.ISomeInterface { }

        [Export, MEFv1.Export]
        [PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative)]
        [MEFv1.PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative)]
        public class PartWithEnumValueMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("Type", typeof(IAdornmentLayer))]
        public class ExportingWithTypeMetadata { }

        [Export, MEFv1.Export]
        [MultipleTypeMetadata(typeof(IAdornmentLayer))]
        [MultipleTypeMetadata(typeof(AllColorableItemInfo))]
        public class ExportingWithMultipleTypeMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("AdornmentLayerType", typeof(IAdornmentLayer), IsMultiple = false)]
        public class ExportingWithTypeSingleMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("Position", AdornmentPositioningBehavior.TextRelative)]
        public class ExportingWithEnumMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("AdornmentPositioningBehavior", AdornmentPositioningBehavior.TextRelative)]
        [MEFv1.ExportMetadata("SmartTagState", SmartTagState.Expanded)]
        public class ExportingWithMultipleDifferentEnumMetadata { }

        [Export, MEFv1.Export]
        [MEFv1.ExportMetadata("AdornmentPositioningBehavior", AdornmentPositioningBehavior.TextRelative)]
        [MEFv1.ExportMetadata("SmartTagState", SmartTagState.Expanded)]
        [MEFv1.ExportMetadata("AdornmentLayerType", typeof(IAdornmentLayer)) ]
        [MultipleTypeMetadata(typeof(IAdornmentLayer))]
        [MultipleTypeMetadata(typeof(AllColorableItemInfo))]
        [PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative)]
        [MEFv1.PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative)]
        public class ExportingWithLotsOfMetadata { }

        public class ExportingWithExportingMembers
        {
            [MEFv1.Export]
            [MEFv1.ExportMetadata("AdornmentLayer", typeof(IAdornmentLayer))]
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

                // fast path
                if (x.CodeBase == y.CodeBase)
                {
                    return true;
                }

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
