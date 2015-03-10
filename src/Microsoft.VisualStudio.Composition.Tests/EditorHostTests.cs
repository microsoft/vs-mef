namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.Composition.Hosting;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text.Editor;
    using Xunit;
    using Xunit.Abstractions;
    using MefV1 = System.ComponentModel.Composition;

    public class EditorHostTests
    {
        private const string EditorAssemblyNames = @"
            Microsoft.VisualStudio.Platform.VSEditor, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Text.Logic, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Text.UI, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Text.UI.Wpf, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Language.StandardClassification, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            StandaloneUndo, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9578fa20308cb27d
        ";

        private readonly ITestOutputHelper output;

        public EditorHostTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [MefFact(CompositionEngines.V1Compat, EditorAssemblyNames, typeof(DummyKeyboardTrackingService))]
        public void ComposeEditor(IContainer container)
        {
            var editorFactory = container.GetExportedValue<ITextEditorFactoryService>();

            container.GetExportedValue<ICompletionBroker>();
            container.GetExportedValue<ISignatureHelpBroker>();
            container.GetExportedValue<ISmartTagBroker>();
            container.GetExportedValue<IQuickInfoBroker>();
        }

        /// <summary>
        /// Automated perf tests are notoriously unstable. This doesn't really verify anything.
        /// It just provides a method to run for collecting traces.
        /// </summary>
        [Fact(Skip = "Not really a test")]
        public async Task ComposeEditorPerformance()
        {
            var editorAssemblies = EditorAssemblyNames
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(p => Assembly.Load(p)).ToArray();
            var v1AssemblyCatalogs = editorAssemblies.Select(p => (MefV1.Primitives.ComposablePartCatalog)new MefV1.Hosting.AssemblyCatalog(p));
            var v1Catalog = new MefV1.Hosting.AggregateCatalog(v1AssemblyCatalogs.Concat(new[] { new TypeCatalog(typeof(DummyKeyboardTrackingService)) }));

            var v3Discovery = new AttributedPartDiscoveryV1();
            var v3Catalog = ComposableCatalog.Create()
                .WithDesktopSupport()
                .WithParts(await v3Discovery.CreatePartsAsync(editorAssemblies))
                .WithPart(v3Discovery.CreatePart(typeof(DummyKeyboardTrackingService)));
            var v3Configuration = CompositionConfiguration.Create(v3Catalog);
            var v3ExportProviderFactory = v3Configuration.CreateExportProviderFactory();

            var v1Timer = new Stopwatch();
            var v3Timer = new Stopwatch();
            const int iterations = 10;

            for (int i = 0; i < iterations; i++)
            {
                v1Timer.Start();
                var v1Container = new CompositionContainer(v1Catalog);
                this.ComposeEditor(new TestUtilities.V1ContainerWrapper(v1Container));
                v1Timer.Stop();

                v3Timer.Start();
                var v3Container = v3ExportProviderFactory.CreateExportProvider();
                this.ComposeEditor(new TestUtilities.V3ContainerWrapper(v3Container, v3Configuration));
                v3Timer.Stop();
            }

            this.output.WriteLine("V1 time per iteration: {0}", v1Timer.ElapsedMilliseconds / iterations);
            this.output.WriteLine("V3 time per iteration: {0}", v3Timer.ElapsedMilliseconds / iterations);
        }

        [MefV1.Export(typeof(IWpfKeyboardTrackingService))]
        public class DummyKeyboardTrackingService : IWpfKeyboardTrackingService
        {
            public void BeginTrackingKeyboard(IntPtr handle, IList<uint> messagesToCapture)
            {
            }

            public void EndTrackingKeyboard()
            {
            }
        }
    }
}
