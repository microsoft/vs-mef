# Visual Studio Managed Extensibility Framework Documentation

VS-MEF is a fast MEF engine supporting System.ComponentModel.Composition and System.Composition.

[![NuGet package](https://img.shields.io/nuget/v/Microsoft.VisualStudio.Composition.svg)](https://www.nuget.org/packages/Microsoft.VisualStudio.Composition)

```console
dotnet add package Microsoft.VisualStudio.Composition
```

## Highlights

* A faster host for existing MEF parts
* Reuse existing MEF attributes
* `ExportFactory<T>` support for scoped sub-containers

## Getting started

[Get started](docs/getting-started.md) with the VS-MEF packages.

## Documentation

* [Why VS-MEF?](docs/why.md)
* [Differences between .NET MEF, NuGet MEF and VS MEF](docs/mef_library_differences.md)
* [Hosting](docs/hosting.md)
* [Authoring MEF parts](docs/parts.md)
* [Dynamic recomposition](docs/dynamic_recomposition.md)
* [Diagnostic Analyzers](analyzers/index.md)
* [vsmefx tool](docs/vsmefx.md)
