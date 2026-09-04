// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Benchmarks;

using System.Composition;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

/// <summary>
/// Measures steady-state activation of representative MEF part graphs.
/// </summary>
[MemoryDiagnoser]
[CPUUsageDiagnoser]
public class ActivationBenchmarks
{
    private ExportFactory<BoundaryPart> boundaryFactory = null!;
    private ExportProvider exportProvider = null!;

    /// <summary>
    /// Gets or sets a value indicating whether expression compilation is enabled.
    /// </summary>
    [Params(false, true)]
    public bool EnableExpressionCompilation { get; set; }

    /// <summary>
    /// Creates and warms the export provider used by the activation benchmarks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var resolver = Resolver.DefaultInstance;
        var discovery = new AttributedPartDiscovery(resolver, isNonPublicSupported: true);
        var discoveredParts = discovery.CreatePartsAsync(
            typeof(SingletonPart),
            typeof(TransientPart),
            typeof(CombinedPart),
            typeof(SharedDependency),
            typeof(TransientDependency),
            typeof(ComplexPart),
            typeof(ComplexServiceA),
            typeof(ComplexServiceB),
            typeof(ComplexServiceC),
            typeof(ComplexSubObjectA),
            typeof(ComplexSubObjectB),
            typeof(ComplexSubObjectC),
            typeof(PropertyPart),
            typeof(PropertyServiceA),
            typeof(PropertyServiceB),
            typeof(PropertyServiceC),
            typeof(PropertySubObjectA),
            typeof(PropertySubObjectB),
            typeof(PropertySubObjectC),
            typeof(ImportManyPart),
            typeof(AdapterA),
            typeof(AdapterB),
            typeof(AdapterC),
            typeof(AdapterD),
            typeof(AdapterE),
            typeof(BoundaryOwner),
            typeof(BoundaryPart),
            typeof(BoundaryLeafA),
            typeof(BoundaryLeafB),
            typeof(BoundaryLeafC),
            typeof(BoundaryLeafD),
            typeof(BoundaryLeafE),
            typeof(BoundaryLeafF),
            typeof(BoundaryDependency)).GetAwaiter().GetResult();

        var catalog = ComposableCatalog.Create(resolver).AddParts(discoveredParts);
        ExportProviderFactoryOptions options = this.EnableExpressionCompilation
            ? ExportProviderFactoryOptions.EnableActivationExpressionCompilation
            : ExportProviderFactoryOptions.None;
        this.exportProvider = CompositionConfiguration.Create(catalog)
            .CreateExportProviderFactory(options)
            .CreateExportProvider();
        this.boundaryFactory = this.exportProvider.GetExportedValue<BoundaryOwner>().Factory;

        this.Singleton();
        this.Transient();
        this.Combined();
        this.Complex();
        this.Property();
        this.ImportMany();
        this.Boundary();
        this.Boundary();
    }

    /// <summary>
    /// Disposes the export provider after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.exportProvider.Dispose();

    /// <summary>
    /// Resolves an already initialized shared part.
    /// </summary>
    /// <returns>The last resolved part.</returns>
    [Benchmark(OperationsPerInvoke = 3)]
    public object Singleton()
    {
        _ = this.exportProvider.GetExportedValue<SingletonPart>();
        _ = this.exportProvider.GetExportedValue<SingletonPart>();
        return this.exportProvider.GetExportedValue<SingletonPart>();
    }

    /// <summary>
    /// Resolves a non-shared part with no imports.
    /// </summary>
    /// <returns>The last resolved part.</returns>
    [Benchmark(OperationsPerInvoke = 3)]
    public object Transient()
    {
        _ = this.exportProvider.GetExportedValue<TransientPart>();
        _ = this.exportProvider.GetExportedValue<TransientPart>();
        return this.exportProvider.GetExportedValue<TransientPart>();
    }

    /// <summary>
    /// Resolves a non-shared part with shared and non-shared constructor imports.
    /// </summary>
    /// <returns>The last resolved part.</returns>
    [Benchmark(OperationsPerInvoke = 3)]
    public object Combined()
    {
        _ = this.exportProvider.GetExportedValue<CombinedPart>();
        _ = this.exportProvider.GetExportedValue<CombinedPart>();
        return this.exportProvider.GetExportedValue<CombinedPart>();
    }

    /// <summary>
    /// Resolves an acyclic non-shared constructor-import graph.
    /// </summary>
    /// <returns>The last resolved part.</returns>
    [Benchmark(OperationsPerInvoke = 3)]
    public object Complex()
    {
        _ = this.exportProvider.GetExportedValue<ComplexPart>();
        _ = this.exportProvider.GetExportedValue<ComplexPart>();
        return this.exportProvider.GetExportedValue<ComplexPart>();
    }

    /// <summary>
    /// Resolves a non-shared graph composed with property imports.
    /// </summary>
    /// <returns>The last resolved part.</returns>
    [Benchmark(OperationsPerInvoke = 3)]
    public object Property()
    {
        _ = this.exportProvider.GetExportedValue<PropertyPart>();
        _ = this.exportProvider.GetExportedValue<PropertyPart>();
        return this.exportProvider.GetExportedValue<PropertyPart>();
    }

    /// <summary>
    /// Resolves a non-shared part with a constructor collection import.
    /// </summary>
    /// <returns>The last resolved part.</returns>
    [Benchmark(OperationsPerInvoke = 3)]
    public object ImportMany()
    {
        _ = this.exportProvider.GetExportedValue<ImportManyPart>();
        _ = this.exportProvider.GetExportedValue<ImportManyPart>();
        return this.exportProvider.GetExportedValue<ImportManyPart>();
    }

    /// <summary>
    /// Activates and disposes a shared graph in a newly created sharing boundary.
    /// </summary>
    /// <returns>The last activated boundary part.</returns>
    [Benchmark(OperationsPerInvoke = 3)]
    public object Boundary()
    {
        _ = this.CreateBoundaryPart();
        _ = this.CreateBoundaryPart();
        return this.CreateBoundaryPart();
    }

    private object CreateBoundaryPart()
    {
        using Export<BoundaryPart> export = this.boundaryFactory.CreateExport();
        return export.Value;
    }

    [Export]
    [Shared]
    private sealed class SingletonPart
    {
    }

    [Export]
    private sealed class TransientPart
    {
    }

    [Export]
    private sealed class CombinedPart
    {
        [ImportingConstructor]
        internal CombinedPart(SharedDependency sharedDependency, TransientDependency transientDependency)
        {
        }
    }

    [Export]
    [Shared]
    private sealed class SharedDependency
    {
    }

    [Export]
    private sealed class TransientDependency
    {
    }

    [Export]
    private sealed class ComplexPart
    {
        [ImportingConstructor]
        internal ComplexPart(
            ComplexServiceA serviceA,
            ComplexServiceB serviceB,
            ComplexServiceC serviceC,
            ComplexSubObjectA subObjectA,
            ComplexSubObjectB subObjectB,
            ComplexSubObjectC subObjectC)
        {
        }
    }

    [Export]
    private sealed class ComplexServiceA
    {
    }

    [Export]
    private sealed class ComplexServiceB
    {
    }

    [Export]
    private sealed class ComplexServiceC
    {
    }

    [Export]
    private sealed class ComplexSubObjectA
    {
        [ImportingConstructor]
        internal ComplexSubObjectA(ComplexServiceA service)
        {
        }
    }

    [Export]
    private sealed class ComplexSubObjectB
    {
        [ImportingConstructor]
        internal ComplexSubObjectB(ComplexServiceB service)
        {
        }
    }

    [Export]
    private sealed class ComplexSubObjectC
    {
        [ImportingConstructor]
        internal ComplexSubObjectC(ComplexServiceC service)
        {
        }
    }

    [Export]
    private sealed class PropertyPart
    {
        [Import]
        internal PropertyServiceA ServiceA { get; set; } = null!;

        [Import]
        internal PropertyServiceB ServiceB { get; set; } = null!;

        [Import]
        internal PropertyServiceC ServiceC { get; set; } = null!;

        [Import]
        internal PropertySubObjectA SubObjectA { get; set; } = null!;

        [Import]
        internal PropertySubObjectB SubObjectB { get; set; } = null!;

        [Import]
        internal PropertySubObjectC SubObjectC { get; set; } = null!;
    }

    [Export]
    [Shared]
    private sealed class PropertyServiceA
    {
    }

    [Export]
    [Shared]
    private sealed class PropertyServiceB
    {
    }

    [Export]
    [Shared]
    private sealed class PropertyServiceC
    {
    }

    [Export]
    private sealed class PropertySubObjectA
    {
        [Import]
        internal PropertyServiceA Service { get; set; } = null!;
    }

    [Export]
    private sealed class PropertySubObjectB
    {
        [Import]
        internal PropertyServiceB Service { get; set; } = null!;
    }

    [Export]
    private sealed class PropertySubObjectC
    {
        [Import]
        internal PropertyServiceC Service { get; set; } = null!;
    }

    [Export]
    private sealed class ImportManyPart
    {
        [ImportingConstructor]
        internal ImportManyPart([ImportMany] IEnumerable<IAdapter> adapters)
        {
        }
    }

    private interface IAdapter
    {
    }

    [Export(typeof(IAdapter))]
    private sealed class AdapterA : IAdapter
    {
    }

    [Export(typeof(IAdapter))]
    private sealed class AdapterB : IAdapter
    {
    }

    [Export(typeof(IAdapter))]
    private sealed class AdapterC : IAdapter
    {
    }

    [Export(typeof(IAdapter))]
    private sealed class AdapterD : IAdapter
    {
    }

    [Export(typeof(IAdapter))]
    private sealed class AdapterE : IAdapter
    {
    }

    [Export, Shared]
    private sealed class BoundaryOwner
    {
        [Import, SharingBoundary("BenchmarkBoundary")]
        internal ExportFactory<BoundaryPart> Factory { get; set; } = null!;
    }

    [Export, Shared("BenchmarkBoundary")]
    private sealed class BoundaryPart
    {
        [ImportingConstructor]
        internal BoundaryPart(
            BoundaryLeafA leafA,
            BoundaryLeafB leafB,
            BoundaryLeafC leafC,
            BoundaryLeafD leafD,
            BoundaryLeafE leafE,
            BoundaryLeafF leafF)
        {
        }

        [Import]
        internal BoundaryDependency Dependency { get; set; } = null!;
    }

    [Export]
    private sealed class BoundaryLeafA
    {
        [ImportingConstructor]
        internal BoundaryLeafA(BoundaryDependency dependency)
        {
        }
    }

    [Export]
    private sealed class BoundaryLeafB
    {
        [ImportingConstructor]
        internal BoundaryLeafB(BoundaryDependency dependency)
        {
        }
    }

    [Export]
    private sealed class BoundaryLeafC
    {
        [ImportingConstructor]
        internal BoundaryLeafC(BoundaryDependency dependency)
        {
        }
    }

    [Export]
    private sealed class BoundaryLeafD
    {
        [ImportingConstructor]
        internal BoundaryLeafD(BoundaryDependency dependency)
        {
        }
    }

    [Export]
    private sealed class BoundaryLeafE
    {
        [ImportingConstructor]
        internal BoundaryLeafE(BoundaryDependency dependency)
        {
        }
    }

    [Export]
    private sealed class BoundaryLeafF
    {
        [ImportingConstructor]
        internal BoundaryLeafF(BoundaryDependency dependency)
        {
        }
    }

    [Export, Shared("BenchmarkBoundary")]
    private sealed class BoundaryDependency
    {
    }
}
