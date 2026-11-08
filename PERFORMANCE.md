# Performance

This document records performance investigations, implemented optimizations, and possible future work.

## Activation benchmarks

`test/Microsoft.VisualStudio.Composition.Benchmarks/ActivationBenchmarks.cs` covers six steady-state activation shapes with expression compilation both disabled and enabled:

- Retrieval of a shared part.
- Activation of a non-shared part without imports.
- Activation with shared and non-shared constructor imports.
- Activation of a larger acyclic constructor-import graph.
- Activation of a property-import graph.
- Activation with an `ImportMany` constructor parameter.

Run these benchmarks with:

```powershell
dotnet run --project test\Microsoft.VisualStudio.Composition.Benchmarks\Microsoft.VisualStudio.Composition.Benchmarks.csproj -c Release --framework net10.0 -- --filter "*ActivationBenchmarks*"
```

Benchmark results should be considered together with startup time, allocation data, and the number of generated or JIT-compiled methods.

## Low-JIT-cost optimizations

The low-JIT-cost optimizations deliberately avoid adding expression-compiled activation methods. They include:

- Caching exact runtime export lookups for `GetExportedValue<T>()`.
- Caching initialized shared root values without using `Lazy<T>`, which would break supported reentrant activation.
- Bypassing general lifecycle state transitions for non-shared parts that have no imports or `OnImportsSatisfied` callbacks.
- Avoiding LINQ and empty-array allocations while resolving constructor arguments through the normal lifecycle engine.

These changes primarily improve shared export retrieval and simple non-shared activation. They preserve the existing reflection and lifecycle behavior for imported graphs.

## Compiled activation experiment

The `perf/compiled-activation-plans` branch preserves the expression-compilation work separately. It adds:

- An explicit `EnableActivationExpressionCompilation` export provider factory option. Compilation is disabled by default.
- Compiled constructor delegates only for repeatedly activated non-shared parts and parts shared within named sharing boundaries.
- Factory-scoped activation counts and delegate caches so repeated sharing-boundary instances can amortize compilation.
- Tiering that keeps the first activation on the reflection path and compiles a constructor only when it is activated again.
- Factory-scoped, provider-independent activation plans that are reused across repeated sharing-boundary instances.
- Plan execution against the current provider and lifecycle tracker so boundary-local sharing, cycles, and disposal ownership remain isolated.
- Weak provider-keyed shared-value lookups so reusable plans do not retain disposed boundary instances.
- A minimum plan size for named-boundary parts so small graphs remain on the lower-cost constructor-delegate path.
- Fused construction and member-satisfaction delegates for roots exercised repeatedly through `ExportFactory<T>` or `ExportFactory<T, TMetadata>`.
- A maximum of 64 constructor/member operations per fused factory island.
- Compiled property and field setters.
- Recursive direct activation plans for supported non-shared subgraphs and named-boundary roots.
- Direct construction of constructor imports, property imports, and array or `IEnumerable<T>` imports.

Unsupported cases fall back to the normal lifecycle engine. These include non-shared cycles, disposable parts, open generic parts, lazy imports, export factories, exported members, custom collections, and `OnImportsSatisfied`. Shared edges continue through the lifecycle engine, preserving boundary-local sharing and property-import cycles.

The compiled approach substantially improves throughput and allocations, but it also creates many additional generated methods that must be JIT-compiled. A short BenchmarkDotNet run on one machine produced the following indicative results:

| Scenario | Compilation disabled | Compilation enabled |
| --- | ---: | ---: |
| Shared | 15.33 ns, 0 B | 15.69 ns, 0 B |
| Simple non-shared | 472.66 ns, 160 B | 36.32 ns, 24 B |
| Constructor imports | 1,772.14 ns, 712 B | 69.65 ns, 88 B |
| Complex constructor graph | 8,022.69 ns, 3,176 B | 258.23 ns, 408 B |
| Property imports | 5,846.28 ns, 2,440 B | 596.97 ns, 136 B |
| `ImportMany` | 4,271.04 ns, 1,440 B | 231.00 ns, 240 B |
| Repeated complex sharing boundary | 12.39 us, 8,992 B | 10.75 us, 6,406 B |

These numbers came from separate BenchmarkDotNet short and medium runs and are intended to show the magnitude of the tradeoff, not to establish release-quality baselines.

In a medium BenchmarkDotNet run, fusing the representative factory island preserved throughput within measurement noise compared with the separate-delegate implementation (10.68 us versus 10.75 us) while reducing allocations from 6.63 KB to 6.26 KB. The island requires two generated methods for its construction and member-satisfaction phases instead of eight separate constructor and setter methods. One additional small constructor delegate remains for a lifecycle-managed dependency outside the fused island.

Application-wide shared parts are excluded from compilation because they normally activate only once. Compilation is limited to:

- Non-shared parts that may be activated repeatedly.
- Parts shared within a sharing boundary that is instantiated repeatedly.
- Eager subgraphs rooted at either of those part categories.

## Future activation work

Future work should be incremental and should measure JIT and startup costs as first-class outcomes.

### 1. Hybrid per-part plans

The current experimental direct plan is all-or-nothing: one unsupported node rejects the complete eager activation closure. A hybrid plan could pre-resolve imports and optimize supported steps while delegating unsupported edges to the existing lifecycle engine.

This would let ordinary constructor and property activation benefit even when a larger graph includes cycles, disposables, lazy imports, export factories, callbacks, or generic parts. Keeping lifecycle trackers at this stage should limit semantic risk.

### 2. Selective compilation

Expression compilation should be restricted to parts with a credible opportunity to amortize its startup and JIT cost. Likely candidates are:

- Non-shared parts observed or expected to be activated repeatedly.
- Parts shared in a repeatedly created sharing boundary.

Application-wide shared parts should normally remain on the reflection path. Selection may be based on composition metadata and sharing policy rather than compiling every eligible part eagerly.

### 3. Further fused eager subgraph work

The experiment now builds typed delegates only for roots exercised repeatedly through an export factory. This is not the whole application graph. The closure stops at existing shared values, lazy imports, nested export factories, unsupported lifecycle edges, and other deferred boundaries.

The fused delegates use typed locals, direct constructor calls, direct member assignments, and typed collection creation. This eliminates intermediate `object[]` arrays, recursive `Func<object?>` dispatch, repeated casts, and reflection-based collection assignment within the supported island.

Further work could segment plans rather than rejecting islands that exceed the current operation budget, and could support more lifecycle edges without expanding the fused method excessively.

## Required measurements

Any compiled or hybrid proposal should compare:

- Provider creation and first-activation time.
- Steady-state activation throughput.
- Managed allocations.
- Number and size of generated methods.
- JIT time and generated native code size.
- Behavior on representative Visual Studio compositions and repeated sharing-boundary activation.
- Functional compatibility for cycles, reentrancy, disposal ownership, exceptions, generic closing, and `OnImportsSatisfied`.
