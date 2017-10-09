# Why VS MEF?

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

Learn more about [the differences between .NET MEF, NuGet MEF, and this library][Differences].

[MEFv2Pkg]: https://www.nuget.org/packages/system.composition
[Differences]: mef_library_differences.md
