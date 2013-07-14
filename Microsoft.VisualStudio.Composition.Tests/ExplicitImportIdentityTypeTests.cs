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

    public class ExplicitImportIdentityTypeTests
    {
        [MefFact(CompositionEngines.V1, typeof(UpcastingExplicitImporter), typeof(Implementor))]
        public void UpcastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<UpcastingExplicitImporter>();
            Assert.IsType<Implementor>(explicitImporter.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1, typeof(DowncastingExplicitImporter), typeof(Implementor))]
        public void DowncastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<DowncastingExplicitImporter>();
            Assert.NotNull(explicitImporter.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1, typeof(LazyUpcastingExplicitImporter), typeof(Implementor))]
        public void LazyUpcastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<LazyUpcastingExplicitImporter>();
            Assert.IsType<Implementor>(explicitImporter.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1, typeof(LazyDowncastingExplicitImporter), typeof(Implementor))]
        public void LazyDowncastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<LazyDowncastingExplicitImporter>();
            Assert.NotNull(explicitImporter.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1, typeof(ImportManyLazyUpcastingExplicitImporter), typeof(Implementor))]
        public void ImportManyLazyUpcastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<ImportManyLazyUpcastingExplicitImporter>();
            Assert.IsType<Implementor>(explicitImporter.ImportingProperty.Single().Value);
        }

        [MefFact(CompositionEngines.V1, typeof(ImportManyLazyDowncastingExplicitImporter), typeof(Implementor))]
        public void ImportManyLazyDowncastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<ImportManyLazyDowncastingExplicitImporter>();
            Assert.NotNull(explicitImporter.ImportingProperty.Single().Value);
        }

        [MefFact(CompositionEngines.V1, typeof(ImportManyUpcastingExplicitImporter), typeof(Implementor))]
        public void ImportManyUpcastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<ImportManyUpcastingExplicitImporter>();
            Assert.IsType<Implementor>(explicitImporter.ImportingProperty.Single());
        }

        [MefFact(CompositionEngines.V1, typeof(ImportManyDowncastingExplicitImporter), typeof(Implementor))]
        public void ImportManyDowncastingExplicitImportType(IContainer container)
        {
            var explicitImporter = container.GetExportedValue<ImportManyDowncastingExplicitImporter>();
            Assert.NotNull(explicitImporter.ImportingProperty.Single());
        }

        [MefV1.Export]
        public class UpcastingExplicitImporter
        {
            [MefV1.Import(typeof(ISomeType))]
            public object ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class DowncastingExplicitImporter
        {
            [MefV1.Import(typeof(ISomeType))]
            public Implementor ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class LazyUpcastingExplicitImporter
        {
            [MefV1.Import(typeof(ISomeType))]
            public Lazy<object> ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class LazyDowncastingExplicitImporter
        {
            [MefV1.Import(typeof(ISomeType))]
            public Lazy<Implementor> ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class ImportManyLazyUpcastingExplicitImporter
        {
            [MefV1.ImportMany(typeof(ISomeType))]
            public IEnumerable<Lazy<object>> ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class ImportManyLazyDowncastingExplicitImporter
        {
            [MefV1.ImportMany(typeof(ISomeType))]
            public IEnumerable<Lazy<Implementor>> ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class ImportManyUpcastingExplicitImporter
        {
            [MefV1.ImportMany(typeof(ISomeType))]
            public IEnumerable<object> ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class ImportManyDowncastingExplicitImporter
        {
            [MefV1.ImportMany(typeof(ISomeType))]
            public IEnumerable<Implementor> ImportingProperty { get; set; }
        }

        public interface ISomeType { }

        [MefV1.Export(typeof(ISomeType))]
        public class Implementor : ISomeType { }
    }
}
