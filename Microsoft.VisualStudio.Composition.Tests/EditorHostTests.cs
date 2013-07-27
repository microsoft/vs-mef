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
    using Microsoft.VisualStudio.Text.Editor;
    using Xunit;

    public class EditorHostTests
    {
        private const string EditorAssemblyNames = @"
            Microsoft.VisualStudio.Platform.VSEditor, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Text.Logic, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Text.UI, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Text.UI.Wpf, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Microsoft.VisualStudio.Language.StandardClassification, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
        ";

        [MefFact(CompositionEngines.V1, EditorAssemblyNames, Skip = "Not yet passing")]
        public void ComposeEditor(IContainer container)
        {
            var editorFactory = container.GetExportedValue<ITextEditorFactoryService>();
        }
    }
}
