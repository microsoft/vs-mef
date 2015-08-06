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
        public async Task GetAssemblyInputs_IdentifiesAssembliesWithOnlyExportMetadata()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(ExportingOnlyMetadataType)));

            var expected = new HashSet<AssemblyName>(AssemblyNameComparer.Default)
            {
                typeof(Microsoft.VisualStudio.Text.Editor.AdornmentPositioningBehavior).Assembly.GetName(),
                typeof(ExportingOnlyMetadataType).Assembly.GetName(),
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
        [PartMetadata("ExternalAssemblyValue", Microsoft.VisualStudio.Text.Editor.AdornmentPositioningBehavior.TextRelative )]
        [MEFv1.PartMetadata("ExternalAssemblyValue", Microsoft.VisualStudio.Text.Editor.AdornmentPositioningBehavior.TextRelative)]
        public class ExportingOnlyMetadataType { }

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
