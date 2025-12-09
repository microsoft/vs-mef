// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF007DuplicateImportAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF007DuplicateImportAnalyzerTests
{
    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning()
    {
        string test = """
            class Foo
            {
                public string Value1 { get; set; }
                public string Value2 { get; set; }
                private int field1;
                private int field2;

                public Foo(string value1, string value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithNoImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithSingleImport_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDifferentTypeImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string StringValue { get; set; }

                [Import]
                public int IntValue { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDuplicateTypeImports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string {|VSMEF007:Value1|} { get; set; }

                [Import]
                public string {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithDuplicateImportsWithImportAttributes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:value1|}, [Import] string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithDuplicateImportsWithoutImportAttributes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string {|VSMEF007:value1|}, string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithDuplicateImportsWithMixedImportAttributes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:value1|}, string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMixedPropertyAndConstructorDuplicates_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string {|VSMEF007:PropertyValue|} { get; set; }

                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:constructorValue|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDifferentContractNames_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import("Contract1")]
                public string Value1 { get; set; }

                [Import("Contract2")]
                public string Value2 { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithSameContractNames_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import("SameContract")]
                public string {|VSMEF007:Value1|} { get; set; }

                [Import("SameContract")]
                public string {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMefV2Attributes_Warning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string {|VSMEF007:Value1|} { get; set; }

                [Import]
                public string {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithDifferentTypeImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string stringValue, [Import] int intValue)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithDifferentContractNames_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("Contract1")] string value1, [Import("Contract2")] string value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithSameContractNames_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("SameContract")] string {|VSMEF007:value1|}, [Import("SameContract")] string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithThreeDuplicateImports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:value1|}, [Import] string {|VSMEF007:value2|}, [Import] string {|VSMEF007:value3|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithAllImplicitImports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:importedValue1|}, string {|VSMEF007:implicitImport|}, [Import] string {|VSMEF007:importedValue2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithContractTypeParameter_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(typeof(string))] object {|VSMEF007:value1|}, [Import(typeof(string))] object {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonImportingConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo(string value1, string value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithImportManyAttributes_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] IEnumerable<string> values1, [ImportMany] IEnumerable<int> values2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithMixedParameterTypesAndContracts_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string defaultImport, [Import("CustomContract")] string customImport)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameContractTypeButDifferentParameterTypes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(typeof(IService))] object {|VSMEF007:service1|}, [Import(typeof(IService))] object {|VSMEF007:service2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameTypeAndDifferentAttributes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(AllowDefault = true)] string {|VSMEF007:import1|}, string {|VSMEF007:import2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportingConstructorWithImplicitImports_Warning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string {|VSMEF007:value1|}, string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithImplicitAndExplicitSameContract_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string {|VSMEF007:implicitString|}, [Import(typeof(string))] object {|VSMEF007:explicitString|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithDifferentContractNames_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("Contract1")] string value1, [Import("Contract2")] string value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithImplicitAndDifferentExplicitContract_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string implicitString, [Import("CustomStringContract")] string explicitString)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithDifferentContractNameAndType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace Foo
            {
                class A { }
            }

            [Export]
            class Bar
            {
                [ImportingConstructor]
                public Bar([Import("Foo.A")] string s, Foo.A a)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameContractNameDifferentTypes_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("MyContract")] string value1, [Import("MyContract")] int value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameTypeDifferentContractNames_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("Contract1")] string value1, [Import("Contract2")] string value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithExplicitContractNameAndTypeMatching_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace MyNamespace
            {
                class MyType { }
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("MyNamespace.MyType", typeof(MyNamespace.MyType))] MyNamespace.MyType {|VSMEF007:value1|}, MyNamespace.MyType {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyImportsWithDifferentContractNameButSameType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace MyNamespace
            {
                class MyType { }
            }

            [Export]
            class Foo
            {
                [Import("CustomContract")]
                public MyNamespace.MyType Value1 { get; set; }

                [Import]
                public MyNamespace.MyType Value2 { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithTypeofContractTypeButDifferentName_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("CustomName", typeof(IService))] object service1, [Import(typeof(IService))] object service2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameOpenGenericButDifferentTypeArguments_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string> stringBar, IBar<int> intBar)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportPropertiesWithSameOpenGenericButDifferentTypeArguments_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [Import]
                public IBar<string> StringBar { get; set; }

                [Import]
                public IBar<int> IntBar { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithMultipleGenericTypeArgumentsDistinct_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T, U>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string, int> stringIntBar, IBar<int, string> intStringBar, IBar<bool, double> boolDoubleBar)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameClosedGeneric_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string> {|VSMEF007:stringBar1|}, IBar<string> {|VSMEF007:stringBar2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportingConstructorWithSameOpenGenericButDifferentTypeArguments_NoWarning()
    {
        string test = """
            using System.Composition;

            interface IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string> stringBar, IBar<int> intBar)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportPropertiesWithNestedGenerics_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            interface IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [Import]
                public IBar<List<string>> StringListBar { get; set; }

                [Import]
                public IBar<List<int>> IntListBar { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithOpenGenericExportAndDistinctClosedImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T>
            {
            }

            [Export(typeof(IBar<>))]
            class Bar<T> : IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string> stringBar, IBar<int> intBar)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportPropertiesWithOpenGenericExportAndDistinctClosedImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T>
            {
            }

            [Export(typeof(IBar<>))]
            class Bar<T> : IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [Import]
                public IBar<string> StringBar { get; set; }

                [Import]
                public IBar<int> IntBar { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithOpenGenericExportAndMultipleDistinctClosedImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T, U>
            {
            }

            [Export(typeof(IBar<,>))]
            class Bar<T, U> : IBar<T, U>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string, int> stringIntBar, IBar<int, string> intStringBar, IBar<bool, double> boolDoubleBar)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithOpenGenericExportAndSameClosedImports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IBar<T>
            {
            }

            [Export(typeof(IBar<>))]
            class Bar<T> : IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string> {|VSMEF007:stringBar1|}, IBar<string> {|VSMEF007:stringBar2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportingConstructorWithOpenGenericExportAndDistinctClosedImports_NoWarning()
    {
        string test = """
            using System.Composition;

            interface IBar<T>
            {
            }

            [Export(typeof(IBar<>))]
            class Bar<T> : IBar<T>
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(IBar<string> stringBar, IBar<int> intBar)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportsWithRequiredCreationPolicyNonShared_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public string Value1 { get; set; }

                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public string Value2 { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorImportsWithRequiredCreationPolicyNonShared_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(
                    [Import(RequiredCreationPolicy = CreationPolicy.NonShared)] string value1,
                    [Import(RequiredCreationPolicy = CreationPolicy.NonShared)] string value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MixedCreationPolicyImports_WarningOnlyForNonNonShared()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public string NonShared1 { get; set; }

                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public string NonShared2 { get; set; }

                [Import]
                public string {|VSMEF007:Shared1|} { get; set; }

                [Import]
                public string {|VSMEF007:Shared2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SingleRegularImportAndSingleNonSharedImport_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string RegularImport { get; set; }

                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public string NonSharedImport { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:Value1|} { get; set; }

                [Import]
                public Lazy<string> {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyImportsWithDifferentUnderlyingTypes_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> StringValue { get; set; }

                [Import]
                public Lazy<int> IntValue { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyAndNonLazyWithSameUnderlyingType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:LazyValue|} { get; set; }

                [Import]
                public string {|VSMEF007:DirectValue|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportFactoryImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public ExportFactory<string> {|VSMEF007:Factory1|} { get; set; }

                [Import]
                public ExportFactory<string> {|VSMEF007:Factory2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportFactoryImportsWithDifferentUnderlyingTypes_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public ExportFactory<string> StringFactory { get; set; }

                [Import]
                public ExportFactory<int> IntFactory { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportFactoryAndDirectImportWithSameUnderlyingType_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public ExportFactory<string> {|VSMEF007:Factory|} { get; set; }

                [Import]
                public string {|VSMEF007:DirectValue|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyAndExportFactoryWithSameUnderlyingType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:LazyValue|} { get; set; }

                [Import]
                public ExportFactory<string> {|VSMEF007:Factory|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyWithMetadataImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IMetadata { }

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string, IMetadata> {|VSMEF007:Value1|} { get; set; }

                [Import]
                public Lazy<string, IMetadata> {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyWithMetadataAndLazyWithoutMetadata_SameType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IMetadata { }

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string, IMetadata> {|VSMEF007:ValueWithMetadata|} { get; set; }

                [Import]
                public Lazy<string> {|VSMEF007:ValueWithoutMetadata|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportFactoryWithMetadataImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IMetadata { }

            [Export]
            class Foo
            {
                [Import]
                public ExportFactory<string, IMetadata> {|VSMEF007:Factory1|} { get; set; }

                [Import]
                public ExportFactory<string, IMetadata> {|VSMEF007:Factory2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithLazyImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(Lazy<string> {|VSMEF007:value1|}, Lazy<string> {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithLazyAndDirectImportsSameType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(Lazy<string> {|VSMEF007:lazyValue|}, string {|VSMEF007:directValue|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithExportFactoryImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(ExportFactory<string> {|VSMEF007:factory1|}, ExportFactory<string> {|VSMEF007:factory2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MixedPropertyAndConstructorLazyImports_SameType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:PropertyValue|} { get; set; }

                [ImportingConstructor]
                public Foo(Lazy<string> {|VSMEF007:constructorValue|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MixedPropertyLazyAndConstructorDirect_SameType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:PropertyValue|} { get; set; }

                [ImportingConstructor]
                public Foo(string {|VSMEF007:constructorValue|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2LazyImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System;
            using System.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:Value1|} { get; set; }

                [Import]
                public Lazy<string> {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyImportsWithDifferentContractNames_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import("Contract1")]
                public Lazy<string> Value1 { get; set; }

                [Import("Contract2")]
                public Lazy<string> Value2 { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportFactoryImportsWithDifferentContractNames_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import("Contract1")]
                public ExportFactory<string> Factory1 { get; set; }

                [Import("Contract2")]
                public ExportFactory<string> Factory2 { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ThreeLazyImportsWithSameUnderlyingType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:Value1|} { get; set; }

                [Import]
                public Lazy<string> {|VSMEF007:Value2|} { get; set; }

                [Import]
                public Lazy<string> {|VSMEF007:Value3|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyExportFactoryAndDirect_AllSameType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public Lazy<string> {|VSMEF007:LazyValue|} { get; set; }

                [Import]
                public ExportFactory<string> {|VSMEF007:Factory|} { get; set; }

                [Import]
                public string {|VSMEF007:DirectValue|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyWithGenericInterface_SameClosedType_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IService<T> { }

            [Export]
            class Foo
            {
                [Import]
                public Lazy<IService<string>> {|VSMEF007:Value1|} { get; set; }

                [Import]
                public Lazy<IService<string>> {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyWithGenericInterface_DifferentClosedTypes_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IService<T> { }

            [Export]
            class Foo
            {
                [Import]
                public Lazy<IService<string>> StringService { get; set; }

                [Import]
                public Lazy<IService<int>> IntService { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyWithNonSharedCreationPolicy_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public Lazy<string> Value1 { get; set; }

                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public Lazy<string> Value2 { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportFactoryWithNonSharedCreationPolicy_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public ExportFactory<string> {|VSMEF007:Factory1|} { get; set; }

                [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
                public ExportFactory<string> {|VSMEF007:Factory2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
