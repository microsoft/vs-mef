namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text.Editor;
    using Xunit;
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

        [MefFact(CompositionEngines.V1Compat, EditorAssemblyNames, typeof(DummyKeyboardTrackingService))]
        public void ComposeEditor(IContainer container)
        {
            var editorFactory = container.GetExportedValue<ITextEditorFactoryService>();

            container.GetExportedValue<ICompletionBroker>();
            container.GetExportedValue<ISignatureHelpBroker>();
            container.GetExportedValue<ISmartTagBroker>();
            container.GetExportedValue<IQuickInfoBroker>();
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
