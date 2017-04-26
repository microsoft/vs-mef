# VS MEF (Visual Studio's flavor of the Managed Extensibility Framework)

[![NuGet package](https://img.shields.io/nuget/v/Microsoft.VisualStudio.Composition.svg)](https://nuget.org/packages/Microsoft.VisualStudio.Composition)
[![Build status](https://ci.appveyor.com/api/projects/status/q4uavk7qso20cd9t/branch/master?svg=true)](https://ci.appveyor.com/project/AArnott/vs-mef/branch/master)

## Why VS MEF?

The MEF that ships with the .NET Framework (System.ComponentModel.Composition) is good,
and Visual Studio used it through Dev12 (Visual Studio 2013).
But it had performance limitations inherent in its "dynamic composition" capability,
which Visual Studio did not require, and Visual Studio needed to surpass the performance
that ".NET MEF" could offer.

The .NET team went on to create an all new implementation of MEF, which was "portable",
and shipped in a NuGet package called Microsoft.Composition. This was faster in some
respects than the .NET Framework, but lacked the extensibility Visual Studio required,
was incompatible with MEF parts written for ".NET MEF", and suffered from poor startup
performance. This new MEF implementation was later renamed to [System.Composition][MEFv2Pkg],
but has otherwise not received much by way of upgrades.

VS MEF was created to reach performance benchmarks beyond .NET MEF's reach, to meet
the demanding requirements of Visual Studio's heavy use of MEF for the editor and the
Common Project System (CPS). Roslyn wanted to use MEF that would work both in portable
scenarios and Visual Studio, so VS MEF was designed to bridge the gap between .NET MEF
and NuGet MEF so that MEF parts written for either system could run under VS MEF and
share a common composition such that a NuGet MEF part could import the exports offered
by a .NET MEF part, and vice versa.

VS MEF utilizes a fully precomputed and validated composition graph for maximum throughput
when constructing MEF exports. This also produces a complete list of compositional diagnostics
that describe MEF parts that were rejected from the graph with root causes and cascading effects
identified.

Both VS MEF's catalog and composition can be serialized after being created, and
later deserialized in a subsequent instance of the application for very fast startup time
that does not require loading assemblies, scanning them, or computing the composition.

Notwithstanding it's name and original purpose, VS MEF is a library that can run
independently of Visual Studio. Its design is to be hostable by unit tests and other
applications with similar requirements and has appeared in a variety of such Microsoft
applications already.

## Differences between .NET MEF and NuGet MEF

VS MEF achieves very high compatibility with two prior MEF libraries that were not
themselves compatible with each other. They each have a unique set of attributes, interfaces
and patterns which may look similar but have contradicting default behaviors and more subtle
but important differences.

### MEF parts in the catalog

VS MEF models its parts catalog such that it can describe all the behaviors of both systems.
It has an extensible part discovery system, with two discovery modules built-in:
one that understands .NET MEF parts and another for NuGet MEF parts.
Each part discovery module can contribute parts to a common VS MEF catalog, after which
MEF parts no longer retain affinity to a particular MEF variety and are considered compatible
with each other.

### Catalog hierarchies vs. sharing boundaries

.NET MEF and NuGet MEF both had their own way to express "sub-scopes" of containers.
.NET MEF's story requires custom MEF hosting code that is aware of each scope, making it
impossible for a MEF part itself to define a new scope. Each scope is backed by a unique
catalog.
NuGet MEF's story utilizes what it calls "sharing boundaries" and gave MEF parts this freedom
to create sub-scopes. The scopes are discovered automatically based on MEF attributes.

VS MEF follows NuGet MEF's model and supports sub-scopes through NuGet MEF attributes.
To define a MEF part that can create new sub-scope instances, that MEF part must use
NuGet MEF attributes as only they can describe sharing boundaries.

### Caveats

* A given MEF part must use a single variety of MEF attributes. Mixing a .NET MEF `Export`
  attribute with a NuGet MEF `ExportMetadata` attribute will result in a MEF part that exports
  without the export metadata, because each part discovery module produces part descriptors
  based on the MEF attributes it was designed to understand.
* .NET MEF offered a `CompositionContainer.Compose` method which leveraged its dynamic
  recomposition feature to add a part to the graph after the container was instantiated.
  VS MEF offers no such facility. The catalog must be complete before the graph is created.
  Note that `SatisfyImportsOnce` functionality is available in VS MEF and is often a reasonable
  substitute.

## Hosting MEF

Hosting VS MEF requires a catalog of parts, a composition (i.e. a graph) where those MEF parts
are nodes and their imports/exports are edges between them, and an export provider that
owns an instance of the graph, and the lifetime of instantiated MEF parts.

In VS MEF, the catalog and composition API is immutable and supports a fluent syntax to
generate efficient new copies with modifications applied. The first element that is mutable
is the `ExportProvider` returned from an `ExportProviderFactory`.

### Hosting MEF in a closed (non-extensible) application

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

### Hosting MEF in an extensible application

To have more control over the MEF catalog or to allow extensibility after you ship your app,
you can install the [Microsoft.VisualStudio.Composition][VSMEFPkg] NuGet package:

    Install-Package Microsoft.VisualStudio.Composition

Then host VS MEF with code like this:

```csharp
// Prepare part discovery to support both flavors of MEF attributes.
var discovery = PartDiscovery.Combine(
    new AttributedPartDiscovery(Resolver.DefaultInstance), // "NuGet MEF" attributes (Microsoft.Composition)
    new AttributedPartDiscoveryV1(Resolver.DefaultInstance)); // ".NET MEF" attributes (System.ComponentModel.Composition)

// Build up a catalog of MEF parts
var catalog = ComposableCatalog.Create(Resolver.DefaultInstance)
    .AddParts(discovery.CreatePartsAsync(Assembly.GetExecutingAssembly()).Result)
    .WithDesktopSupport() // Adds desktop-only features such as metadata view interface support
    .WithCompositionService(); // Makes an ICompositionService export available to MEF parts to import

// Assemble the parts into a valid graph.
var config = CompositionConfiguration.Create(catalog);

// Prepare an ExportProvider factory based on this graph.
var epf = config.CreateExportProviderFactory();

// Create an export provider, which represents a unique container of values.
// You can create as many of these as you want, but typically an app needs just one.
var exportProvider = epf.CreateExportProvider();

// Obtain our first exported value
var program = exportProvider.GetExportedValue<Program>();
```

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

[AppHostPkg]: https://www.nuget.org/packages/Microsoft.VisualStudio.Composition.AppHost
[VSMEFPkg]: https://www.nuget.org/packages/Microsoft.VisualStudio.Composition
[MEFv2Pkg]: https://www.nuget.org/packages/system.composition
