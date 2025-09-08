# vs-mef

[![Build Status](https://dev.azure.com/azure-public/vside/_apis/build/status/vs-mef)](https://dev.azure.com/azure-public/vside/_build/latest?definitionId=17)
[![codecov](https://codecov.io/gh/Microsoft/vs-mef/branch/main/graph/badge.svg)](https://codecov.io/gh/Microsoft/vs-mef)

This repo contains the Visual Studio team's implementation of .NET's managed extensibility framework.
It is broken up into several NuGet packages, as listed below.

## Microsoft.VisualStudio.Composition

[![NuGet package](https://img.shields.io/nuget/v/Microsoft.VisualStudio.Composition.svg)](https://www.nuget.org/packages/Microsoft.VisualStudio.Composition)

Lightning fast MEF engine, supporting System.ComponentModel.Composition and System.Composition.

### Features

* A new, faster host for your existing MEF parts
* Reuse the MEF attributes you're already using
* `ExportFactory<T>` support to create sub-containers with scoped lifetime (i.e. sharing boundaries)

### Documentation

* [Getting started](https://microsoft.github.io/vs-mef/docs/getting-started.html)
* [Why VS-MEF?](https://microsoft.github.io/vs-mef/docs/why.html)
* [Differences between .NET MEF, NuGet MEF and VS MEF](https://microsoft.github.io/vs-mef/docs/mef_library_differences.html)
* [Hosting](https://microsoft.github.io/vs-mef/docs/hosting.html)

[Learn more about this package](src/Microsoft.VisualStudio.Composition/README.md).

## Microsoft.VisualStudio.Composition.Analyzers

[![NuGet package](https://img.shields.io/nuget/v/Microsoft.VisualStudio.Composition.Analyzers.svg)](https://www.nuget.org/packages/Microsoft.VisualStudio.Composition.Analyzers)

Analyzers for MEF consumers to help identify common errors in MEF parts.

[Learn more about this package](src/Microsoft.VisualStudio.Composition.Analyzers/README.md).

## Microsoft.VisualStudio.Composition.AppHost

[![NuGet package](https://img.shields.io/nuget/v/Microsoft.VisualStudio.Composition.AppHost.svg)](https://www.nuget.org/packages/Microsoft.VisualStudio.Composition.AppHost)

Adds a VS MEF system with a pre-computed, cached MEF graph.

[Learn more about this package](src/Microsoft.VisualStudio.Composition.AppHost/README.md).

## Microsoft.VisualStudio.Composition.VSMefx

A diagnostic tool to understand catalogs, compositions and diagnose issues in them.

[Learn more about this package](src/Microsoft.VisualStudio.Composition.VSMefx/README.md).
