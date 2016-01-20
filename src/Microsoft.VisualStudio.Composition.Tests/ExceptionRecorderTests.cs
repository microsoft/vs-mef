namespace Microsoft.VisualStudio.Composition.Tests
{
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class ExceptionRecorderTests
    {
        private readonly ITestOutputHelper output;

        public ExceptionRecorderTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task CreateExportProvider_ThrowsArgumentNullException()
        {
            var discovery = TestUtilities.V2Discovery;
            List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
            parts.Add(discovery.CreatePart(typeof(ExportWithFailingConstructor)));

            var exportProviderFactory = await this.CreateExportProviderFactoryAsync(parts);
            Assert.Throws<ArgumentNullException>(() => exportProviderFactory.CreateExportProvider(null));
        }

        [Fact]
        public async Task CreateExportProviderDefaultArgs_StillThrowsException()
        {
            var discovery = TestUtilities.V2Discovery;
            List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
            parts.Add(discovery.CreatePart(typeof(ClassWithExportingMemberThatFails)));
            parts.Add(discovery.CreatePart(typeof(ExportWithFailingConstructor)));
            parts.Add(discovery.CreatePart(typeof(FailingExport1)));
            parts.Add(discovery.CreatePart(typeof(FailingExport2)));
            parts.Add(discovery.CreatePart(typeof(ExportWithLazyImportsOfBadExports)));

            var exportProviderFactory = await this.CreateExportProviderFactoryAsync(parts);
            Assert.NotNull(exportProviderFactory);

            var exportProvider = exportProviderFactory.CreateExportProvider();
            Assert.NotNull(exportProvider);

            var exportWithLazyImport = exportProvider.GetExportedValue<ExportWithLazyImportsOfBadExports>();
            Assert.NotNull(exportWithLazyImport);
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingConstructor.Value);
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingExportingMember.Value);
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingExports.ElementAt(0).Value);
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingExports.ElementAt(1).Value);
        }

        [Fact]
        public async Task RecordException_CalledWhenPartWithExceptionThrown()
        {
            var discovery = TestUtilities.V2Discovery;
            List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
            parts.Add(discovery.CreatePart(typeof(ClassWithExportingMemberThatFails)));
            parts.Add(discovery.CreatePart(typeof(ExportWithFailingConstructor)));
            parts.Add(discovery.CreatePart(typeof(FailingExport1)));
            parts.Add(discovery.CreatePart(typeof(FailingExport2)));
            parts.Add(discovery.CreatePart(typeof(ExportWithLazyImportsOfBadExports)));

            var exportProviderFactory = await this.CreateExportProviderFactoryAsync(parts);
            Assert.NotNull(exportProviderFactory);

            int timesCallbackIsCalled = 0;
            Mock<IExceptionRecorder> mockExceptionRecorder = new Mock<IExceptionRecorder>();
            mockExceptionRecorder.Setup(x => x
                .RecordException(
                    It.IsAny<Exception>(),
                    It.IsNotNull<RuntimeComposition.RuntimeImport>(),
                    It.IsNotNull<RuntimeComposition.RuntimeExport>()))
                .Callback<Exception, RuntimeComposition.RuntimeImport, RuntimeComposition.RuntimeExport>(
                (ex, import, export) =>
                {
                    Assert.NotNull(ex);
                    Assert.NotNull(import);
                    Assert.NotNull(export);

                    timesCallbackIsCalled++;
                });

            var exportProvider = exportProviderFactory.CreateExportProvider(mockExceptionRecorder.Object);
            Assert.NotNull(exportProvider);

            var exportWithLazyImport = exportProvider.GetExportedValue<ExportWithLazyImportsOfBadExports>();
            Assert.NotNull(exportWithLazyImport);
            Assert.False(exportWithLazyImport.FailingConstructor.IsValueCreated);
            Assert.False(exportWithLazyImport.FailingExportingMember.IsValueCreated);
            Assert.All(exportWithLazyImport.FailingExports, ex => Assert.False(ex.IsValueCreated));

            // Evaluating this import should cause the RecordException method to be called
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingConstructor.Value);

            // Evaluating this import should cause the RecordException method to be called
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingExportingMember.Value);

            // Evaluating these imports should cause the RecordException method to be called twice
            Assert.Equal(2, exportWithLazyImport.FailingExports.Count());
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingExports.ElementAt(0).Value);
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingExports.ElementAt(1).Value);

            // We should have recorded 4 exceptions (1 for failing constructor, 1 for failing exporting member,
            // and 2 for both failing exports).
            mockExceptionRecorder.Verify();
            Assert.Equal(4, timesCallbackIsCalled);
        }

        private async Task<IExportProviderFactory> CreateExportProviderFactoryAsync(IEnumerable<ComposablePartDefinition> parts)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(parts);
            var configuration = CompositionConfiguration.Create(catalog);
            var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);
            var cacheManager = new CachedComposition();
            var ms = new MemoryStream();
            await cacheManager.SaveAsync(runtimeComposition, ms);
            this.output.WriteLine("Cache file size: {0}", ms.Length);
            ms.Position = 0;
            var deserializedRuntimeComposition = await cacheManager.LoadRuntimeCompositionAsync(ms, Resolver.DefaultInstance);
            Assert.Equal(runtimeComposition, deserializedRuntimeComposition);

            return runtimeComposition.CreateExportProviderFactory();
        }

        [Export]
        public class ExportWithLazyImportsOfBadExports
        {
            [Import]
            public Lazy<ExportWithFailingConstructor> FailingConstructor { get; set; }

            [Import]
            public Lazy<DummyClass> FailingExportingMember { get; set; }

            [ImportMany]
            public IEnumerable<Lazy<FailingExport>> FailingExports { get; set; }
        }

        [Export]
        public class ExportWithFailingConstructor
        {
            public static readonly Exception ConstructorException = new Exception("ExportWithFailingConstructor");

            public ExportWithFailingConstructor()
            {
                throw ConstructorException;
            }
        }

        public class ClassWithExportingMemberThatFails
        {
            [Export]
            public DummyClass FailingExport
            {
                get
                {
                    throw new Exception("ExportingMemberWithFailingGetter");
                }
            }
        }

        [Export(typeof(FailingExport))]
        public class FailingExport1 : FailingExport
        {
            public static readonly Exception ConstructorException = new Exception("FailingExport1");

            public FailingExport1()
            {
                throw ConstructorException;
            }
        }

        [Export(typeof(FailingExport))]
        public class FailingExport2 : FailingExport
        {
            public static readonly Exception ConstructorException = new Exception("FailingExport1");

            public FailingExport2()
            {
                throw ConstructorException;
            }
        }

        public class FailingExport { }

        public class DummyClass { }
    }
}
