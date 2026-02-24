// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF010ImportManyParameterCollectionTypeAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF010ImportManyParameterCollectionTypeAnalyzerTests
{
    [Fact]
    public async Task ImportingConstructorWithList_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] List<string> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("List<string>", "values"));
    }

    [Fact]
    public async Task ImportingConstructorWithIList_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] IList<string> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("IList<string>", "values"));
    }

    [Fact]
    public async Task ImportingConstructorWithICollection_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] ICollection<string> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("ICollection<string>", "values"));
    }

    [Fact]
    public async Task ImportingConstructorWithHashSet_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] HashSet<string> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("HashSet<string>", "values"));
    }

    [Fact]
    public async Task ImportingConstructorWithListOfLazy_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] List<Lazy<string>> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("List<Lazy<string>>", "values"));
    }

    [Fact]
    public async Task ImportingConstructorMixedParameters_OnlyErrorOnList()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo(
                    [ImportMany] string[] validArray,
                    [ImportMany] IEnumerable<string> validEnumerable,
                    [ImportMany] List<string> {|#0:invalidList|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("List<string>", "invalidList"));
    }

    [Fact]
    public async Task ImportingConstructorWithMultipleInvalidParameters_MultipleErrors()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo(
                    [ImportMany] List<string> {|#0:values1|},
                    [ImportMany] HashSet<int> {|#1:values2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("List<string>", "values1"),
            VerifyCS.Diagnostic(VSMEF010ImportManyParameterCollectionTypeAnalyzer.Descriptor)
                .WithLocation(1)
                .WithArguments("HashSet<int>", "values2"));
    }
}
