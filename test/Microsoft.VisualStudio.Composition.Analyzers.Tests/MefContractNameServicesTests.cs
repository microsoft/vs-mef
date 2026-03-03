// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Composition.Analyzers;

public class MefContractNameServicesTests
{
    private static readonly Lazy<MetadataReference[]> TrustedPlatformAssemblyReferences = new(() =>
    {
        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        Assert.False(string.IsNullOrEmpty(trustedAssemblies));

        string[] allAssemblyPaths = trustedAssemblies!
            .Split(Path.PathSeparator)
            .ToArray();

        // Restrict to a minimal reference set to keep Roslyn compilation startup fast and stable in CI.
        HashSet<string> requiredAssemblies = new(StringComparer.OrdinalIgnoreCase)
        {
            "mscorlib",
            "System.Private.CoreLib",
            "System.Runtime",
            "System.Runtime.Extensions",
            "System.Collections",
            "System.Linq",
            "System.Linq.Expressions",
            "netstandard",
            "Microsoft.CSharp",
        };

        string[] filteredAssemblyPaths = allAssemblyPaths
            .Where(path => requiredAssemblies.Contains(Path.GetFileNameWithoutExtension(path)))
            .ToArray();

        string[] referencesToUse = filteredAssemblyPaths.Length > 0 ? filteredAssemblyPaths : allAssemblyPaths;

        return referencesToUse
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    });

    [Fact]
    public void GetTypeIdentity_NullType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MefContractNameServices.GetTypeIdentity(null!));
    }

    [Fact]
    public void GetTypeIdentityFromMethod_NullMethod_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MefContractNameServices.GetTypeIdentityFromMethod(null!));
    }

    [Fact]
    public void GetTypeIdentity_NonGenericNamedType_IncludesNamespace()
    {
        CSharpCompilation compilation = CreateCompilation("namespace N { class Service { } }");
        INamedTypeSymbol serviceType = GetType(compilation, "N.Service");

        string identity = MefContractNameServices.GetTypeIdentity(serviceType);

        Assert.Equal("N.Service", identity);
    }

    [Fact]
    public void GetTypeIdentity_GlobalNamespaceType_ExcludesNamespacePrefix()
    {
        CSharpCompilation compilation = CreateCompilation("class GlobalService { }");
        INamedTypeSymbol serviceType = GetType(compilation, "GlobalService");

        string identity = MefContractNameServices.GetTypeIdentity(serviceType);

        Assert.Equal("GlobalService", identity);
    }

    [Fact]
    public void GetTypeIdentity_NestedType_UsesPlusSeparator()
    {
        CSharpCompilation compilation = CreateCompilation("namespace N { class Outer { class Inner { } } }");
        INamedTypeSymbol innerType = GetType(compilation, "N.Outer+Inner");

        string identity = MefContractNameServices.GetTypeIdentity(innerType);

        Assert.Equal("N.Outer+Inner", identity);
    }

    [Fact]
    public void GetTypeIdentity_GenericClosedNestedType_UsesTypeArgumentsAndPlusSeparator()
    {
        const string source = """
            namespace N
            {
                class Outer<T>
                {
                    public class Inner<U> { }
                }

                class Holder
                {
                    public Outer<int>.Inner<string> Field = null!;
                }
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        IFieldSymbol field = GetField(compilation, "N.Holder", "Field");

        string identity = MefContractNameServices.GetTypeIdentity(field.Type);

        Assert.Equal("N.Outer(System.Int32)+Inner(System.String)", identity);
    }

    [Fact]
    public void GetTypeIdentity_GenericTypeDefinition_FormatsTypeParameterPlaceholders()
    {
        const string source = """
            namespace N
            {
                class Outer<T>
                {
                    public class Inner<U> { }
                }
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        INamedTypeSymbol inner = GetType(compilation, "N.Outer`1+Inner`1");

        string identity = MefContractNameServices.GetTypeIdentity(inner);

        Assert.Equal("N.Outer({0})+Inner({1})", identity);
    }

    [Fact]
    public void GetTypeIdentity_GenericTypeDefinition_WithoutFormattedGenericName_EmitsEmptyGenericSlots()
    {
        const string source = "namespace N { class Outer<T> { } }";
        CSharpCompilation compilation = CreateCompilation(source);
        INamedTypeSymbol outer = GetType(compilation, "N.Outer`1");

        string identity = MefContractNameServices.GetTypeIdentity(outer, formatGenericName: false);

        Assert.Equal("N.Outer()", identity);
    }

    [Fact]
    public void GetTypeIdentity_ArrayAndJaggedArray_FormatsDimensions()
    {
        const string source = """
            class Holder
            {
                public int[][,] Value = null!;
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        IFieldSymbol field = GetField(compilation, "Holder", "Value");

        string identity = MefContractNameServices.GetTypeIdentity(field.Type);

        Assert.Equal("System.Int32[][,]", identity);
    }

    [Fact]
    public void GetTypeIdentity_PointerType_AppendsPointerMarker()
    {
        const string source = "unsafe class Holder { public int* Value; }";
        CSharpCompilation compilation = CreateCompilation(source, allowUnsafe: true);
        IFieldSymbol field = GetField(compilation, "Holder", "Value");

        string identity = MefContractNameServices.GetTypeIdentity(field.Type);

        Assert.Equal("System.Int32*", identity);
    }

    [Fact]
    public void GetTypeIdentity_DynamicType_UsesDynamicKeyword()
    {
        const string source = "class Holder { public dynamic Value = null!; }";
        CSharpCompilation compilation = CreateCompilation(source);
        IFieldSymbol field = GetField(compilation, "Holder", "Value");

        string identity = MefContractNameServices.GetTypeIdentity(field.Type);

        Assert.Equal("dynamic", identity);
    }

    [Fact]
    public void GetTypeIdentity_DelegateType_UsesInvokeSignatureWithByRefMarkers()
    {
        const string source = "public delegate int MyDelegate(ref int x, out string y);";
        CSharpCompilation compilation = CreateCompilation(source);
        INamedTypeSymbol delegateType = GetType(compilation, "MyDelegate");

        string identity = MefContractNameServices.GetTypeIdentity(delegateType);

        Assert.Equal("System.Int32(System.Int32&,System.String&)", identity);
    }

    [Fact]
    public void GetTypeIdentityFromMethod_NoParameters_FormatsEmptyParameterList()
    {
        const string source = "class C { public void M() { } }";
        CSharpCompilation compilation = CreateCompilation(source);
        IMethodSymbol method = GetMethod(compilation, "C", "M");

        string identity = MefContractNameServices.GetTypeIdentityFromMethod(method);

        Assert.Equal("System.Void()", identity);
    }

    [Fact]
    public void GetTypeIdentityFromMethod_GenericParameter_UsesMethodGenericPlaceholder()
    {
        const string source = "class C { public T M<T>(T value) { return value; } }";
        CSharpCompilation compilation = CreateCompilation(source);
        IMethodSymbol method = GetMethod(compilation, "C", "M");

        string identity = MefContractNameServices.GetTypeIdentityFromMethod(method);

        Assert.Equal("{0}({0})", identity);
    }

    private static CSharpCompilation CreateCompilation(string source, bool allowUnsafe = false)
    {
        MetadataReference[] references = TrustedPlatformAssemblyReferences.Value;

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe);

        return CSharpCompilation.Create(
            assemblyName: "ContractNameTests",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: options);
    }

    private static INamedTypeSymbol GetType(CSharpCompilation compilation, string metadataName)
    {
        return compilation.GetTypeByMetadataName(metadataName)
            ?? throw new InvalidOperationException($"Type '{metadataName}' not found.");
    }

    private static IFieldSymbol GetField(CSharpCompilation compilation, string typeMetadataName, string fieldName)
    {
        INamedTypeSymbol type = GetType(compilation, typeMetadataName);
        return type.GetMembers(fieldName).OfType<IFieldSymbol>().Single();
    }

    private static IMethodSymbol GetMethod(CSharpCompilation compilation, string typeMetadataName, string methodName)
    {
        INamedTypeSymbol type = GetType(compilation, typeMetadataName);
        return type.GetMembers(methodName).OfType<IMethodSymbol>().Single();
    }
}
