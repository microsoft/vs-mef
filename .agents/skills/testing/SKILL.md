---
name: testing
description: Provides guidance and instructions for writing, running and filtering tests.
user-invocable: false
---

**IMPORTANT**: This repository uses xUnit v3 with Microsoft.Testing.Platform (MTP v2). Traditional VSTest `--filter` syntax does NOT work. Use the options below instead.

* There should generally be one test project (under the `test` directory) per shipping project (under the `src` directory). Test projects are named after the project being tested with a `.Tests` suffix.
* Tests use xUnit v3 with Microsoft.Testing.Platform (MTP v2).

## Running Tests

**Run all tests**:
```bash
dotnet test --no-build -c Release
```

**Run tests for a specific test project**:
```bash
dotnet test --project test/Microsoft.VisualStudio.Composition.Tests/Microsoft.VisualStudio.Composition.Tests.csproj --no-build -c Release
```

**Run a single test method**:
```bash
dotnet test --project test/Microsoft.VisualStudio.Composition.Tests/Microsoft.VisualStudio.Composition.Tests.csproj --no-build -c Release -- --filter-method ClassName.MethodName
```

**Run all tests in a test class**:
```bash
dotnet test --project test/Microsoft.VisualStudio.Composition.Tests/Microsoft.VisualStudio.Composition.Tests.csproj --no-build -c Release -- --filter-class ClassName
```

**Run tests with wildcard matching** (supports wildcards at beginning and/or end):
```bash
dotnet test --project test/Microsoft.VisualStudio.Composition.Tests/Microsoft.VisualStudio.Composition.Tests.csproj --no-build -c Release -- --filter-method "*Pattern*"
```

**Run tests with a specific trait**:
```bash
dotnet test --project test/Microsoft.VisualStudio.Composition.Tests/Microsoft.VisualStudio.Composition.Tests.csproj --no-build -c Release -- --filter-trait "TraitName=value"
```

**Exclude tests with a specific trait**:
```bash
dotnet test --project test/Microsoft.VisualStudio.Composition.Tests/Microsoft.VisualStudio.Composition.Tests.csproj --no-build -c Release -- --filter-not-trait "TestCategory=FailsInCloudTest"
```

**Run tests for a specific framework only**:
```bash
dotnet test --project test/Microsoft.VisualStudio.Composition.Tests/Microsoft.VisualStudio.Composition.Tests.csproj --no-build -c Release --framework net8.0
```

**List all available tests without running them**:
```bash
cd test/Microsoft.VisualStudio.Composition.Tests
dotnet run --no-build -c Release --framework net8.0 -- --list-tests
```

**Key points about test filtering with xUnit v3 / MTP v2**:
- Options after `--` are passed to the test runner, not to `dotnet test`
- Use `--filter-method`, `--filter-class`, and `--filter-namespace` for simple filtering
- Use `--filter-trait` and `--filter-not-trait` for trait-based filtering
- Traditional VSTest `--filter` expressions do NOT work
- Wildcards `*` are supported at the beginning and/or end of filter values
- See `--help` for the query filter language for advanced scenarios
