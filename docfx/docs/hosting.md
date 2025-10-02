# Hosting VS MEF

If you're not already familiar with MEF in general, start by reading [a MEF introductory article](https://www.bing.com/search?q=introduction%20to%20the%20managed%20extensibility%20framework&qs=n&form=QBRE&sp=-1&pq=undefined&sc=0-30&sk=&cvid=BF12F5C9D203498690B2251D6D841BB4).

Hosting VS MEF requires a catalog of parts, a composition (i.e. a graph) where those MEF parts
are nodes and their imports/exports are edges between them, and an export provider that
owns an instance of the graph, and the lifetime of instantiated MEF parts.

In VS MEF, the catalog and composition API is immutable and supports a fluent syntax to
generate efficient new copies with modifications applied. The first element that is mutable
is the `ExportProvider` returned from an `ExportProviderFactory`, which returns activated
parts with imports satisfied with additional activated parts.

## ComposableCatalog

The <xref:Microsoft.VisualStudio.Composition.ComposableCatalog> type is the first foundational class for hosting VS MEF. It is a collection of all the "MEF parts" (i.e. types) that MEF will activate and initialize for the application.

A MEF part is typically a class that has been decorated with `[Export]` and `[Import]` attributes which will lead MEF to create those exported types and provide values for the imports. A <xref:Microsoft.VisualStudio.Composition.PartDiscovery>-derived class is used to scan these types and create MEF parts in the form of <xref:Microsoft.VisualStudio.Composition.ComposablePartDefinition> instances to add to a <xref:Microsoft.VisualStudio.Composition.ComposableCatalog>. This can be done for individual types, but is more commonly done on all the types in an assembly or even across many assemblies.

Since both .NET MEF and NuGet MEF define their own `[Export]` and `[Import]` attributes, VS MEF defines a <xref:Microsoft.VisualStudio.Composition.PartDiscovery>-derived class to support each set of attributes. VS MEF even lets you combine multiple <xref:Microsoft.VisualStudio.Composition.PartDiscovery> instances into one so that your application can contain a mix of MEF parts that are defined with either set of attributes.

## Hosting MEF in an extensible application

To have control over the MEF catalog or to allow extensibility after you ship your app,
you can install the [Microsoft.VisualStudio.Composition][VSMEFPkg] NuGet package:

    Install-Package Microsoft.VisualStudio.Composition

Then host VS MEF with code like this:

[!code-csharp[](../../samples/docs/Hosting.cs#Extensible)]

When composing the graph from the catalog with `CompositionConfiguration.Create`,
errors in the graph may be detected. MEF parts that introduce errors (e.g.
cardinality mismatches) are rejected from the graph. The rejection of these parts
can lead to a cascade of other cardinality mismatches and more parts being rejected.
When this recursive process is done and all the invalid parts are rejected, what
remains in the composition is a fully valid graph that should only fail at runtime
if a MEF part throws an exception during construction.

MEF "rejection" leading to incomplete graphs is a bug in the application or an extension.
You can choose to throw when any error occurs by calling `config.ThrowOnErrors()`,
where `config` is as defined in the example above.
Or you can choose to let the app continue to run, but produce an error report that
can be analyzed when necessary. To obtain the diagnostics report from VS MEF,
inspect the `config.CompositionErrors` collection, which is in the form of a stack
where the top element represents the root causes and each subsequent element in the
stack represents a cascade of failures that resulted from rejecting the original
defective MEF parts. Usually when debugging MEF failures, the first level errors
are the ones to focus on. But listing them all can be helpful to answer the question
of "Why is export *X* missing?" since X itself may not be defective but may have been
rejected in the cascade.

## Hosting MEF in a closed (non-extensible) application

The easiest way to do it, and get a MEF cache for faster startup to boot, just
install the [Microsoft.VisualStudio.Composition.AppHost][AppHostPkg] NuGet package:

    Install-Package Microsoft.VisualStudio.Composition.AppHost

Installing this package adds assembly references to VS MEF and adds a step to your build
to create a MEF catalog cache that your app can load at runtime to avoid the startup hit
of loading and scanning all your MEF assemblies. The entire composition is cached, making
acquiring your first export as fast as possible.

Then with just 3 lines of code (which the package presents to you) you can bootstrap MEF
and get your first export:

```csharp
var exportProviderFactory = ExportProviderFactory.LoadDefault();
var exportProvider = exportProviderFactory.CreateExportProvider();
var program = exportProvider.GetExportedValue<Program>();
```

## Migrating from .NET MEF or NuGet MEF

### Where is <xref:System.ComponentModel.Composition.Hosting.DirectoryCatalog>?

VS MEF has no directory catalog. But you can scan a directory for its assemblies and add each assembly to a single <xref:Microsoft.VisualStudio.Composition.ComposableCatalog> with code such as this:

[!code-csharp[](../../samples/docs/Hosting.cs#DirectoryCatalog)]

[AppHostPkg]: https://www.nuget.org/packages/Microsoft.VisualStudio.Composition.AppHost
[VSMEFPkg]: https://www.nuget.org/packages/Microsoft.VisualStudio.Composition
