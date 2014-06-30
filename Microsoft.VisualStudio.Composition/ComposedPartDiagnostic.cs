namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ComposedPartDiagnostic
    {
        public ComposedPartDiagnostic(ComposedPart part, string formattedMessage)
        {
            Requires.NotNull(part, "part");
            Requires.NotNullOrEmpty(formattedMessage, "formattedMessage");

            this.Part = part;
            this.Message = formattedMessage;
        }

        public ComposedPartDiagnostic(ComposedPart part, string unformattedMessage, params object[] args)
            : this(part, string.Format(CultureInfo.CurrentCulture, unformattedMessage, args))
        {
        }

        public ComposedPart Part { get; private set; }

        public string Message { get; private set; }
    }
}
