// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Originally copied from https://github.com/eiriktsarpalis/PolyType.Roslyn/blob/main/src/PolyType.Roslyn/SourceWriter.cs
namespace Microsoft.VisualStudio.Composition.Analyzers.CSharp;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// A utility class for generating indented source code.
/// </summary>
internal class SourceWriter
{
    // Standardize on this because generated sources should not vary by platform.
    private const string NewLine = "\r\n";

    private readonly StringBuilder builder = new();
    private int indentation;

    public SourceWriter()
    {
        this.IndentationChar = '\t';
        this.CharsPerIndentation = 1;
    }

    public SourceWriter(char indentationChar, int charsPerIndentation)
    {
        if (!char.IsWhiteSpace(indentationChar))
        {
            throw new ArgumentOutOfRangeException(nameof(indentationChar));
        }

        if (charsPerIndentation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(charsPerIndentation));
        }

        this.IndentationChar = indentationChar;
        this.CharsPerIndentation = charsPerIndentation;
    }

    public char IndentationChar { get; }

    public int CharsPerIndentation { get; }

    public int Length => this.builder.Length;

    public int Indentation
    {
        get => this.indentation;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            this.indentation = value;
        }
    }

    public void WriteLine(char value)
    {
        this.AddIndentation();
        this.builder.Append(value);
        this.builder.Append(NewLine);
    }

    public void WriteLine([StringSyntax("c#-test")] string text, bool disableIndentation = false)
    {
        if (this.indentation == 0 || disableIndentation)
        {
            this.builder.Append(text);
            this.builder.Append(NewLine);
            return;
        }

        bool isFinalLine;
        ReadOnlySpan<char> remainingText = text.AsSpan();
        do
        {
            ReadOnlySpan<char> nextLine = GetNextLine(ref remainingText, out isFinalLine);
            this.AddIndentation();
            this.builder.Append(nextLine.ToString());
            this.builder.Append(NewLine);
        }
        while (!isFinalLine);
    }

    public void WriteLine() => this.builder.Append(NewLine);

    public SourceText ToSourceText()
    {
        if (this.builder.Length == 0)
        {
            throw new InvalidOperationException("Nothing was written.");
        }

        if (this.indentation != 0)
        {
            throw new InvalidOperationException($"Indentation was not balanced. Current indentation: {this.indentation}.");
        }

        return SourceText.From(this.builder.ToString(), Encoding.UTF8);
    }

    private static ReadOnlySpan<char> GetNextLine(ref ReadOnlySpan<char> remainingText, out bool isFinalLine)
    {
        if (remainingText.IsEmpty)
        {
            isFinalLine = true;
            return default;
        }

        int lineLength = remainingText.IndexOf('\n');
        ReadOnlySpan<char> rest;
        if (lineLength == -1)
        {
            lineLength = remainingText.Length;
            isFinalLine = true;
            rest = default;
        }
        else
        {
            rest = remainingText[(lineLength + 1)..];
            isFinalLine = false;
        }

        if ((uint)lineLength > 0 && remainingText[lineLength - 1] == '\r')
        {
            lineLength--;
        }

        ReadOnlySpan<char> next = remainingText[..lineLength];
        remainingText = rest;
        return next;
    }

    private void AddIndentation() => this.builder.Append(this.IndentationChar, this.CharsPerIndentation * this.indentation);
}
