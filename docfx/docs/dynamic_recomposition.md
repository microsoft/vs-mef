# Dynamic Recomposition

VS-MEF does not support .NET MEF's dynamic recomposition feature. This feature is exposed in .NET MEF in several ways, including:

1. The <xref:System.ComponentModel.Composition.Hosting.CompositionContainer.Compose*?displayProperty=nameWithType> and <xref:System.ComponentModel.Composition.AttributedModelServices.ComposeParts*?displayProperty=nameWithType> method.
1. Manipulating the <xref:System.ComponentModel.Composition.Hosting.ExportProvider> collection backing a <xref:System.ComponentModel.Composition.Hosting.CompositionContainer> after it is created.
1. A custom <xref:System.ComponentModel.Composition.Hosting.ExportProvider> whose exported values change over time.

## Catalog manipulation via ExportProviders

VS-MEF has no equivalent for manipulating a catalog
behind a live container. A possibly compatible alternative is to create one VS-MEF catalog with all parts in it, where all exports are decorated with export metadata to describe when it is appropriate to consume the value. Then use import filters or <xref:System.ComponentModel.Composition.ImportManyAttribute> with metadata and review metadata before activating values.

## Adding parts directly with CompositionContainer.Compose

Use of <xref:System.ComponentModel.Composition.Hosting.CompositionContainer.Compose*?displayProperty=nameWithType> tends to fall into either of two categories: a type with exports or a type with only imports.

For a type with only imports, you can trivially replace your use of <xref:System.ComponentModel.Composition.Hosting.CompositionContainer.Compose*?displayProperty=nameWithType> with a call to <xref:System.ComponentModel.Composition.ICompositionService.SatisfyImportsOnce*> which also satisfies all imports on the instance you pass to it as an arument.

For a type with imports and exports, there is no equivalent in VS-MEF to the .NET MEF <xref:System.ComponentModel.Composition.Hosting.CompositionContainer.Compose*> method. If you can add the type that you want to compose to the catalog, that is often the simplest workaround.

For mock testing scenarios where the mocks are dynamically generated types, MEF attributes may not be added to these types. It may also be important to supply pre-instantiated values to the graph rather than have VS-MEF instantiate them. We can address both of these requirements by following this pattern.

First, in your test assembly define and export a type that will expose the instances of your mocks. Supposing you had mocks for `IFoo` and `IBar` that your product assembly imports, you would define this type in your test assembly:

```csharp
using System.ComponentModel.Composition;

[Export]
internal class MockSupplier
{
    [Export]
    internal IFoo Foo { get; set; }

    [Export]
    internal IBar Bar { get; set; }
}
```

At runtime in your test, set up your VS-MEF catalog to include `MockSupplier` along with the product assembly parts that import `IFoo` and `IBar`. Then with the runtime <xref:Microsoft.VisualStudio.Composition.ExportProvider>, initialize the mocks:

```csharp
[Fact]
public void SomeTest()
{
    ExportProvider ep; // TODO: initialize this.

    // Set up the mocks before acquiring other exports.
    var ms = ep.GetExportedValue<MockSupplier>();
    ms.Foo = new Mock<IFoo>().Object;
    ms.Bar = new Mock<IBar>().Object;

    var productExport = this.ep.GetExportedValue<IProduct>();
    // test logic.
}
```

In this way, you control the creation and exported types of mock values created within your tests.

When the test queries for an export that requires activating a part that imports an IFoo, then MEF would activate the `MockSupplier` and query its exporting property. If you have already set the property's value, the MEF import will be satisfied with that value; otherwise the default value for that property will be used to satisfy the import (`null` in this case).
