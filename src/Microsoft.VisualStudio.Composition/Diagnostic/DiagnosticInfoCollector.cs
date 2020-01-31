// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Diagnostic
{
    using System.Text;

    internal class DiagnosticInfoCollector
    {
        private StringBuilder messages;

        private DiagnosticInfoCollector()
        {
            this.messages = new StringBuilder();
        }

        public static DiagnosticInfoCollector CreateInstance()
        {
            return new DiagnosticInfoCollector();
        }

        public void Collect(string message)
        {
            this.messages.AppendLine(message);
        }

        public string GetAndClearInformation()
        {
            var response = this.messages.ToString();
            this.messages.Clear();

            return response;
        }
    }
}