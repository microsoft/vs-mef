// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

/// <summary>
/// A verifier that tests source code against all VSMEF analyzers simultaneously.
/// Used for tests that verify code produces no diagnostics from any analyzer.
/// </summary>
public static partial class CSharpMultiAnalyzerVerifier
{
    /// <summary>
    /// Verifies that the given source code produces no diagnostics from any VSMEF analyzer.
    /// </summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task VerifyAnalyzerAsync(string source)
    {
        var test = new Test { TestCode = source };
        return test.RunAsync();
    }

    /// <summary>
    /// Verifies that the given source code produces no diagnostics from any VSMEF analyzer,
    /// with optional editor configuration.
    /// </summary>
    /// <param name="source">The source code to analyze.</param>
    /// <param name="editorConfig">Optional editor configuration content.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task VerifyAnalyzerAsync(string source, string? editorConfig)
    {
        var test = new Test { TestCode = source };
        if (editorConfig is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", SourceText.From(editorConfig)));
        }

        return test.RunAsync();
    }
}
