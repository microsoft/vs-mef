# VS MEF (Visual Studio's flavor of the Managed Extensibility Framework)

## Hosting MEF

Hosting VS MEF requires a catalog of parts, a composition (i.e. a graph) where those MEF parts
are nodes and their imports/exports are edges between them, and an export provider that
owns an instance of the graph, and the lifetime of instantiated MEF parts.

In VS MEF, the catalog and composition API is immutable and supports a fluent syntax to
generate efficient new copies with modifications applied. The first element that is mutable
is the `ExportProvider` returned from an `ExportProviderFactory`.

### Hosting MEF in a closed (non-extensible) application

The easiest way to do it, and get a MEF cache for faster startup to boot, just
install the Microsoft.VisualStudio.Composition.AppHost nuget package:
 
    Install-Package Microsoft.VisualStudio.Composition.AppHost

You can get this package from the [VS IDE Real-signed release][PkgFeed] feed.  

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
you can install the Microsoft.VisualStudio.Composition package and host VS MEF with code like this:

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
var config = CompositionConfiguration.Create(catalog);
var epf = config.CreateExportProviderFactory();
var exportProvider = epf.CreateExportProvider();
var program = exportProvider.GetExportedValue<Program>();
```

[PkgFeed]: https://mseng.pkgs.visualstudio.com/_packaging/VSIDEProj-RealSigned-Release/nuget/v3/index.json
