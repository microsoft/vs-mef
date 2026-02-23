// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyAll = CSharpMultiAnalyzerVerifier;

/// <summary>
/// These tests verify that valid code does not trigger any analyzer diagnostics.
/// Each test verifies the source code against all VSMEF analyzers simultaneously.
/// </summary>
public class NoWarningTests
{
    [Fact]
    public async Task ClassWithNoConstructors_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithSingleImportingConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string value) { }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMultipleNonImportingConstructors_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo() { }
                public Foo(string value) { }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithImportingConstructorAndRegularConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo() { }

                [ImportingConstructor]
                public Foo(string value) { }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task StructWithSingleImportingConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            struct Foo
            {
                [ImportingConstructor]
                public Foo(string value) { }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task StructWithImportingConstructorAndRegularConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            struct Foo
            {
                public Foo(bool flag) { }

                [ImportingConstructor]
                public Foo(string value) { }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithoutNullableAnnotations_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string? Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonNullableImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public int Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public int? Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int? Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import]
                public int Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2NullableValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import]
                public int? Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2NullableValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int? Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValueTypeConstructorParameterImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] int value)
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableValueTypeConstructorParameterImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] int? value)
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableDisabledImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable disable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableUnspecifiedImportWithAllowDefault_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning_VSMEF008()
    {
        string test = """
            class Foo
            {
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithoutExplicitContractType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithMatchingContractType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(typeof(string))]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithAssignableContractType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }
            class ServiceImpl : IService { }

            class Foo
            {
                [Import(typeof(ServiceImpl))]
                public IService Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyWithCompatibleContractType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            interface IService { }
            class ServiceImpl : IService { }

            class Foo
            {
                [ImportMany(typeof(ServiceImpl))]
                public IEnumerable<IService> Values { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithLazyWrapper_ContractTypeMatch_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [Import(typeof(IService))]
                public Lazy<IService> Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyWithLazyCollection_ContractTypeMatch_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            interface IService { }

            class Foo
            {
                [ImportMany(typeof(IService))]
                public IEnumerable<Lazy<IService>> Values { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorParameter_ContractTypeMatch_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(typeof(IService))] IService value)
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithObjectMemberType_AnyContractType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [Import(typeof(IService))]
                public object Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2Import_NoContractTypeSupport_NoWarning()
    {
        // MEFv2 doesn't support explicit contract types in the same way
        string test = """
            using System.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithBaseClassContractType_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class BaseClass { }
            class DerivedClass : BaseClass { }

            class Foo
            {
                [Import(typeof(DerivedClass))]
                public BaseClass Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithInterfaceImplementation_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }
            class ServiceImpl : IService { }

            class Foo
            {
                [Import(typeof(ServiceImpl))]
                public IService Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithLazyMetadata_ContractTypeMatch_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IService { }
            interface IMetadata { }

            class Foo
            {
                [Import(typeof(IService))]
                public Lazy<IService, IMetadata> Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning_VSMEF009()
    {
        string test = """
            class Foo
            {
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning_VSMEF010()
    {
        string test = """
            class Foo
            {
                public Foo(string value)
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning_VSMEF011()
    {
        string test = """
            class Foo
            {
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithImportOnly_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithImportManyOnly_NoWarning()
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

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithImportOnlyAndContractName_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import("MyContract")]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithImportManyOnlyAndContractName_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany("MyContract")]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning_VSMEF012()
    {
        string test = """
            class Foo
            {
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportWithoutConfig_NoWarning()
    {
        // By default, no MEF version is disallowed
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ExportWithoutConfig_NoWarning()
    {
        // By default, no MEF version is disallowed
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ImportWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Verifies that an unrecognized value for the editorconfig option does not
    /// cause an unhandled exception and the analyzer silently does nothing.
    /// </summary>
    [Fact]
    public async Task GarbageConfigValue_NoWarningAndNoException()
    {
        string editorConfig = """

            root = true

            [*.cs]
            dotnet_diagnostic.VSMEF012.severity = warning
            dotnet_diagnostic.VSMEF012.allowed_mef_version = garbage

            """;

        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test, editorConfig);
    }

    [Fact]
    public async Task MefV1ImportManyWithoutConfig_NoWarning()
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

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportManyWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ImportingConstructorWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo(string value)
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportingConstructorWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo(string value)
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1PartNotDiscoverableWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [PartNotDiscoverable]
            [Export]
            class Foo
            {
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1PartCreationPolicyWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [PartCreationPolicy(CreationPolicy.Shared)]
            [Export]
            class Foo
            {
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2SharedAttributeWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;

            [Shared]
            [Export]
            class Foo
            {
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportMetadataWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            [ExportMetadata("Key", "Value")]
            class Foo
            {
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleV1AttributesOnSameClass_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            [Export]
            [PartCreationPolicy(CreationPolicy.NonShared)]
            class Foo
            {
                [Import]
                public string SingleValue { get; set; }

                [ImportMany]
                public IEnumerable<string> MultipleValues { get; set; }

                [ImportingConstructor]
                public Foo()
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleV2AttributesOnSameClass_NoWarning()
    {
        string test = """
            using System.Composition;
            using System.Collections.Generic;

            [Export]
            [Shared]
            class Foo
            {
                [Import]
                public string SingleValue { get; set; }

                [ImportMany]
                public IEnumerable<string> MultipleValues { get; set; }

                [ImportingConstructor]
                public Foo()
                {
                }
            }
            """;

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
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

        await VerifyAll.VerifyAnalyzerAsync(test);
    }
}
