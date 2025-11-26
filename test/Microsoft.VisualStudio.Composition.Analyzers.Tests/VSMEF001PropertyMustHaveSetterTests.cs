// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF001PropertyMustHaveSetter, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = VisualBasicCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF001PropertyMustHaveSetter, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF001PropertyMustHaveSetterTests
{
    [Fact]
    public async Task ImportingPropertyWithGetterAndSetter_ProducesNoDiagnostic()
    {
        string test = @"
using System.Composition;

class Test
{
    [Import]
    object SomeProperty { get; set; }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingPropertyWithOnlySetter_ProducesNoDiagnostic()
    {
        string test = @"
using System.Composition;

class Test
{
    [Import]
    object SomeProperty
    {
        set { }
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingPropertyWithOnlyGetter_ProducesDiagnostic()
    {
        string test = @"
using System.Composition;

class Test
{
    [Import]
    object [|SomeProperty|] { get; }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingPropertyWithOnlyGetter_ProducesDiagnostic_VB()
    {
        string test = @"
Imports System.Composition

Class Test
    <Import>
    ReadOnly Property [|SomeProperty|] As Object
End Class";

        await VerifyVB.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task UnattributedPropertyWithOnlyGetter_ProducesNoDiagnostic()
    {
        string test = @"
using System.Composition;

[Export]
class Test
{
    object SomeProperty { get; }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task UnattributedPropertyWithOnlyGetter_ProducesNoDiagnostic_VB()
    {
        string test = @"
Imports System.Composition

Class Test
    ReadOnly Property SomeProperty As Object
End Class";

        await VerifyVB.VerifyAnalyzerAsync(test);
    }
}
