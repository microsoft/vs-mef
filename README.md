# VS MEF (Visual Studio's flavor of the Managed Extensibility Framework)

[![NuGet package](https://img.shields.io/nuget/v/Microsoft.VisualStudio.Composition.svg)](https://nuget.org/packages/Microsoft.VisualStudio.Composition)
[![Build Status](https://dev.azure.com/azure-public/vside/_apis/build/status/vs-mef)](https://dev.azure.com/azure-public/vside/_build/latest?definitionId=17)
[![codecov](https://codecov.io/gh/Microsoft/vs-mef/branch/main/graph/badge.svg)](https://codecov.io/gh/Microsoft/vs-mef)
[![Join the chat at https://gitter.im/vs-mef/Lobby](https://badges.gitter.im/vs-mef/Lobby.svg)](https://gitter.im/vs-mef/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

## Features

* A new, faster host for your existing MEF parts
* Reuse the MEF attributes you're already using
* `ExportFactory<T>` support to create sub-containers with scoped lifetime (i.e. sharing boundaries)

## Documentation

* [Why VS-MEF?](doc/why.md)
* [Differences between .NET MEF, NuGet MEF and VS MEF](doc/mef_library_differences.md)
* [Hosting](doc/hosting.md)
* [more docs](doc/index.md)
