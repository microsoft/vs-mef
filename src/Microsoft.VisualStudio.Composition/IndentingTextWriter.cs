// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class IndentingTextWriter : TextWriter
    {
        private const string Indentation = "    ";
        private readonly TextWriter inner;
        private readonly Stack<string> indentationStack = new Stack<string>();

        internal IndentingTextWriter(TextWriter inner)
        {
            Requires.NotNull(inner, nameof(inner));

            this.inner = inner;
        }

        public override Encoding Encoding
        {
            get { return this.inner.Encoding; }
        }

        internal static IndentingTextWriter Get(TextWriter writer)
        {
            Requires.NotNull(writer, nameof(writer));
            return writer as IndentingTextWriter ?? new IndentingTextWriter(writer);
        }

        public override void WriteLine(string? value)
        {
            foreach (var indent in this.indentationStack)
            {
                this.inner.Write(indent);
            }

            this.inner.WriteLine(value);
        }

        public override void Write(char value)
        {
            this.inner.Write(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.inner.Dispose();
            }

            base.Dispose(disposing);
        }

        internal CancelIndent Indent()
        {
            this.indentationStack.Push(Indentation);
            return new CancelIndent(this);
        }

        internal void Unindent()
        {
            this.indentationStack.Pop();
        }

        internal struct CancelIndent : IDisposable
        {
            private readonly IndentingTextWriter writer;

            internal CancelIndent(IndentingTextWriter writer)
            {
                Requires.NotNull(writer, nameof(writer));
                this.writer = writer;
            }

            public void Dispose()
            {
                if (this.writer != null)
                {
                    this.writer.Unindent();
                }
            }
        }
    }
}
