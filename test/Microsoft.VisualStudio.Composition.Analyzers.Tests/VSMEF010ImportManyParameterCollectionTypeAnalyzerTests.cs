// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF010ImportManyParameterCollectionTypeAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF010ImportManyParameterCollectionTypeAnalyzerTests
{
    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning()
    {
        string test = """
            class Foo
            {
                public Foo(string value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithArray_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] string[] values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithIEnumerable_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] IEnumerable<string> values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

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
    public async Task ImportingConstructorWithImplicitImportManyArray_NoWarning()
    {
        // In MEFv1, constructor parameters are implicitly ImportMany if the type is a collection
        // However, this analyzer only fires on explicit [ImportMany] attributes
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo(string[] values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithImplicitImportManyIEnumerable_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo(IEnumerable<string> values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithImplicitImportManyList_NoWarning()
    {
        // Implicit ImportMany (no explicit [ImportMany] attribute) is not analyzed
        // Users should use explicit [ImportMany] to get proper diagnostics
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo(List<string> values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithLazyArray_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] Lazy<string>[] values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithIEnumerableOfLazy_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] IEnumerable<Lazy<string>> values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
    public async Task MefV2ImportingConstructorWithList_NoWarning()
    {
        // MEFv2 supports various collection types - this analyzer only applies to MEFv1
        string test = """
            using System.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] List<string> values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithExportFactoryArray_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] ExportFactory<string>[] values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithIEnumerableOfExportFactory_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] IEnumerable<ExportFactory<string>> values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonImportingConstructorWithList_NoWarning()
    {
        // Not an importing constructor, so no warning
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                public Foo(List<string> values)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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

    [Fact]
    public async Task PropertyImportManyWithList_NoWarning()
    {
        // This analyzer only applies to constructor parameters, not properties
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public List<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
