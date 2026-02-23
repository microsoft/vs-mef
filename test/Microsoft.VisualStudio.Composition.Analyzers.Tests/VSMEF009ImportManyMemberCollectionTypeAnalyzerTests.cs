// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF009ImportManyMemberCollectionTypeAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF009ImportManyMemberCollectionTypeAnalyzerTests
{
    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning()
    {
        string test = """
            class Foo
            {
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnArray_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportMany]
                public string[] Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnIEnumerable_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnList_NoWarning()
    {
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

    [Fact]
    public async Task ImportManyOnIList_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IList<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnICollection_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public ICollection<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnNonCollection_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportMany]
                public string {|#0:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("string", "The type must be a collection type such as T[], IEnumerable<T>, List<T>, or a type implementing ICollection<T>."));
    }

    [Fact]
    public async Task ImportManyOnLazyOfCollection_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public Lazy<IEnumerable<string>> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("Lazy<IEnumerable<string>>", "Lazy<T> wrapping a collection is not valid for ImportMany. Use a collection of Lazy<T> instead (e.g., IEnumerable<Lazy<T>>)."));
    }

    [Fact]
    public async Task ImportManyOnLazyOfList_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public Lazy<List<string>> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("Lazy<List<string>>", "Lazy<T> wrapping a collection is not valid for ImportMany. Use a collection of Lazy<T> instead (e.g., IEnumerable<Lazy<T>>)."));
    }

    [Fact]
    public async Task ImportManyOnCollectionOfLazy_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<Lazy<string>> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnCollectionOfExportFactory_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<ExportFactory<string>> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnArrayWithSetter_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportMany]
                public string[] Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnArrayWithoutSetter_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportMany]
                public string[] {|#0:Values|} { get; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.ArrayWithoutSetterDescriptor)
                .WithLocation(0)
                .WithArguments("Values"));
    }

    [Fact]
    public async Task ImportManyOnListWithoutSetter_PreInitialized_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public List<string> Values { get; } = new List<string>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnListWithoutSetter_NotPreInitialized_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public List<string> {|#0:Values|} { get; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NotInitializedDescriptor)
                .WithLocation(0)
                .WithArguments("Values", "Add a property initializer (e.g., = new List<T>();) or initialize the property in all constructors."));
    }

    [Fact]
    public async Task ImportManyOnIEnumerableWithoutSetter_Error()
    {
        // IEnumerable<T> doesn't implement ICollection<T>, so can't be pre-initialized
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> {|#0:Values|} { get; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NotInitializedDescriptor)
                .WithLocation(0)
                .WithArguments("Values", "The type must implement ICollection<T> to support pre-initialization."));
    }

    [Fact]
    public async Task ImportManyOnICollectionWithoutSetter_PreInitialized_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public ICollection<string> Values { get; } = new List<string>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnIListWithoutSetter_PreInitialized_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IList<string> Values { get; } = new List<string>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnField_NonCollection_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportMany]
                public string {|#0:value|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("string", "The type must be a collection type such as T[], IEnumerable<T>, List<T>, or a type implementing ICollection<T>."));
    }

    [Fact]
    public async Task ImportManyOnField_ValidCollection_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public List<string> values;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportManyOnNonCollection_Error()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [ImportMany]
                public string {|#0:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("string", "The type must be a collection type such as T[], IEnumerable<T>, List<T>, or a type implementing ICollection<T>."));
    }

    [Fact]
    public async Task MefV2ImportManyOnArray_NoWarning()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [ImportMany]
                public string[] Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyWithInitializedInConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public List<string> Values { get; }

                public Foo()
                {
                    Values = new List<string>();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyWithHashSet_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public HashSet<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyWithCustomCollectionImplementingICollection_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class MyCollection<T> : ICollection<T>
            {
                public int Count => 0;
                public bool IsReadOnly => false;
                public void Add(T item) { }
                public void Clear() { }
                public bool Contains(T item) => false;
                public void CopyTo(T[] array, int arrayIndex) { }
                public bool Remove(T item) => false;
                public IEnumerator<T> GetEnumerator() => throw new System.NotImplementedException();
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
            }

            class Foo
            {
                [ImportMany]
                public MyCollection<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyOnLazyWithMetadataOfCollection_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            interface IMetadata { }

            class Foo
            {
                [ImportMany]
                public Lazy<IEnumerable<string>, IMetadata> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("Lazy<IEnumerable<string>, IMetadata>", "Lazy<T> wrapping a collection is not valid for ImportMany. Use a collection of Lazy<T> instead (e.g., IEnumerable<Lazy<T>>)."));
    }

    [Fact]
    public async Task ImportManyConstructorParam_NonCollection_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] string {|#0:value|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("string", "The type must be a collection type such as T[], IEnumerable<T>, List<T>, or a type implementing ICollection<T>."));
    }

    [Fact]
    public async Task ImportManyConstructorParam_Array_NoWarning()
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
    public async Task ImportManyConstructorParam_IEnumerable_NoWarning()
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
    public async Task ImportManyConstructorParam_List_NoWarning()
    {
        // List<T> is a valid collection type for VSMEF009
        // VSMEF010 handles the MEFv1 constructor restriction separately
        string test = """
            using System.ComponentModel.Composition;
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
    public async Task ImportManyConstructorParam_LazyWrappingCollection_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] Lazy<IEnumerable<string>> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("Lazy<IEnumerable<string>>", "Lazy<T> wrapping a collection is not valid for ImportMany. Use a collection of Lazy<T> instead (e.g., IEnumerable<Lazy<T>>)."));
    }

    [Fact]
    public async Task ImportManyConstructorParam_CollectionOfLazy_NoWarning()
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
    public async Task ImportManyConstructorParam_MefV2_NonCollection_Error()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] string {|#0:value|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF009ImportManyMemberCollectionTypeAnalyzer.NonCollectionDescriptor)
                .WithLocation(0)
                .WithArguments("string", "The type must be a collection type such as T[], IEnumerable<T>, List<T>, or a type implementing ICollection<T>."));
    }

    [Fact]
    public async Task ImportManyConstructorParam_MefV2_Array_NoWarning()
    {
        string test = """
            using System.Composition;

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
    public async Task ImportManyConstructorParam_WithoutImportingConstructor_NoWarning()
    {
        // [ImportMany] on a parameter without [ImportingConstructor] is not analyzed
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                public Foo([ImportMany] string value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
