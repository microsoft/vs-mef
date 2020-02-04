// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Diagnostic
{
    using System;
    using System.Collections.Generic;

    internal class DiagnosticInfoCollector
    {
        private readonly List<string> messages = new List<string>();

        internal void Collect(string message)
        {
            this.messages.Add(message);
        }

        internal string GetAndClearInformation()
        {
            var response = string.Join(Environment.NewLine, this.messages);
            this.messages.Clear();

            return response;
        }
    }
}