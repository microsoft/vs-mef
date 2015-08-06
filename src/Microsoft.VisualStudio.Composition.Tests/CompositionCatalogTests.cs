namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Composition.Reflection;
    using Xunit;
    using MEFv1 = System.ComponentModel.Composition;
    using Text.Editor;
    using Shell.Interop;
    using Language.Intellisense;

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
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithPartMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithPartMetadataType)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(ExportingWithPartMetadataType).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithLazyTypeSingleMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithLazyTypeSingleMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingWithLazyTypeSingleMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithLazyTypeMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithLazyTypeMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingWithLazyTypeMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithMultipleLazyTypeMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithMultipleLazyTypeMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AllColorableItemInfo).Assembly.GetName(),
                typeof(IAdornmentLayer).Assembly.GetName(),
                typeof(ExportingWithMultipleLazyTypeMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithLazyEnumMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithLazyEnumMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(ExportingWithLazyEnumMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithMultipleLazyEnumMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithMultipleLazyEnumMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(ExportingWithMultipleLazyEnumMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithMultipleDifferentLazyEnumMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingWithMultipleDifferentLazyEnumMetadata)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(SmartTagState).Assembly.GetName(),
                typeof(ExportingWithMultipleDifferentLazyEnumMetadata).Assembly.GetName(),
                typeof(object).Assembly.GetName()
            };

            var actual = catalog.GetInputAssemblies();
            Assert.True(expected.SetEquals(actual));
        }

        [Fact]
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithLotsOfMetadata()
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
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithExportingMembers()
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

        public class NonExportingType { }

        [Export, MEFv1.Export]
        public class ExportingType { }

        [Export, MEFv1.Export]
        public class ExportingTypeDerivesFromOtherAssembly : AssemblyDiscoveryTests.NonPart { }

        [Export, MEFv1.Export]
        public class ExportingTypeImplementsFromOtherAssembly : AssemblyDiscoveryTests.ISomeInterface { }

        [Export, MEFv1.Export]
        [PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative )]
        [MEFv1.PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative)]
        public class ExportingWithPartMetadataType { }

        [Export, MEFv1.Export]
        [Type(typeof(IAdornmentLayer))]
        public class ExportingWithLazyTypeMetadata { }

        [Export, MEFv1.Export]
        [Type(typeof(IAdornmentLayer))]
        [Type(typeof(AllColorableItemInfo))]
        public class ExportingWithMultipleLazyTypeMetadata { }

        [Export, MEFv1.Export]
        [TypeSingle(typeof(IAdornmentLayer))]
        public class ExportingWithLazyTypeSingleMetadata { }

        [Export, MEFv1.Export]
        [Enum(AdornmentPositioningBehavior.TextRelative)]
        public class ExportingWithLazyEnumMetadata { }

        [Export, MEFv1.Export]
        [Enum(AdornmentPositioningBehavior.TextRelative)]
        [Enum(AdornmentPositioningBehavior.ViewportRelative)]
        public class ExportingWithMultipleLazyEnumMetadata { }

        [Export, MEFv1.Export]
        [Enum(AdornmentPositioningBehavior.TextRelative)]
        [Enum2(SmartTagState.Expanded)]
        public class ExportingWithMultipleDifferentLazyEnumMetadata { }

        [Export, MEFv1.Export]
        [Enum(AdornmentPositioningBehavior.TextRelative)]
        [Enum(AdornmentPositioningBehavior.TextRelative)]
        [Enum2(SmartTagState.Expanded)]
        [Enum2(SmartTagState.Collapsed)]
        [TypeSingle(typeof(IAdornmentLayer))]
        [Type(typeof(IAdornmentLayer))]
        [Type(typeof(AllColorableItemInfo))]
        [PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative)]
        [MEFv1.PartMetadata("ExternalAssemblyValue", AdornmentPositioningBehavior.TextRelative)]
        public class ExportingWithLotsOfMetadata { }

        public class ExportingWithExportingMembers
        {
            [MEFv1.Export]
            [Type(typeof(IAdornmentLayer))]
            public object Export;
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

        [MetadataAttribute, MEFv1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
        private sealed class TypeAttribute : Attribute
        {
            // This is a positional argument
            public TypeAttribute(Type type)
            {
                Requires.NotNull(type, nameof(type));
                this.MyType = type;
            }

            public Type MyType { get; private set; }
        }

        [MetadataAttribute, MEFv1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
        private sealed class EnumAttribute : Attribute
        {
            // This is a positional argument
            public EnumAttribute(AdornmentPositioningBehavior behavior)
            {
                this.Behavior = behavior;
            }

            public AdornmentPositioningBehavior Behavior { get; private set; }
        }

        [MetadataAttribute, MEFv1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
        private sealed class Enum2Attribute : Attribute
        {
            // This is a positional argument
            public Enum2Attribute(SmartTagState state)
            {
                this.State = state;
            }

            public SmartTagState State { get; private set; }
        }

        [MetadataAttribute, MEFv1.MetadataAttribute]
        private sealed class TypeSingleAttribute : Attribute
        {
            // This is a positional argument
            public TypeSingleAttribute(Type type)
            {
                Requires.NotNull(type, nameof(type));
                this.MyType = type;
            }

            public Type MyType { get; private set; }
        }
    }
}
