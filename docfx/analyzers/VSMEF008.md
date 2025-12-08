# VSMEF008 Import contract type not assignable to member type

When using `[Import(typeof(T))]` or `[ImportMany(typeof(T))]` with an explicit contract type, the contract type must be assignable to the property, field, or parameter type (after unwrapping `Lazy<>`, `ExportFactory<>`, and collection types).

## Cause

An import specifies a `ContractType` that is incompatible with the member type receiving the import. This will cause a composition failure at runtime.

## Rule description

MEF allows you to specify an explicit contract type using `[Import(typeof(T))]` or `[ImportMany(typeof(T))]`. The exports matching this contract must be assignable to the member where the import is declared. If the types are incompatible, composition will fail at runtime.

This analyzer detects the following incompatibilities:

1. **Direct type mismatch**: The contract type is not assignable to the property/parameter type
2. **Lazy wrapper mismatch**: The contract type is not assignable to the `T` in `Lazy<T>` or `Lazy<T, TMetadata>`
3. **ExportFactory wrapper mismatch**: The contract type is not assignable to the `T` in `ExportFactory<T>` or `ExportFactory<T, TMetadata>`
4. **ImportMany element mismatch**: The contract type is not assignable to the element type of the collection

## Examples of violations

### Direct type mismatch

```cs
using System.ComponentModel.Composition;

class Service
{
    [Import(typeof(ILogger))]
    public string Logger { get; set; }  // ❌ ILogger not assignable to string
}
```

### Lazy wrapper mismatch

```cs
using System;
using System.ComponentModel.Composition;

class Service
{
    [Import(typeof(ILogger))]
    public Lazy<IDatabase> Database { get; set; }  // ❌ ILogger not assignable to IDatabase
}
```

### ExportFactory mismatch

```cs
using System.ComponentModel.Composition;

class Service
{
    [Import(typeof(ILogger))]
    public ExportFactory<IDatabase> DatabaseFactory { get; set; }  // ❌ ILogger not assignable to IDatabase
}
```

### ImportMany element mismatch

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [ImportMany(typeof(ILogger))]
    public IEnumerable<IDatabase> Databases { get; set; }  // ❌ ILogger not assignable to IDatabase
}
```

### Constructor parameter mismatch

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([Import(typeof(ILogger))] IDatabase database)  // ❌ ILogger not assignable to IDatabase
    {
    }
}
```

## Valid scenarios (no diagnostic)

### Contract type assignable to member type

```cs
using System.ComponentModel.Composition;

interface ILogger { }
class FileLogger : ILogger { }

class Service
{
    [Import(typeof(ILogger))]
    public ILogger Logger { get; set; }  // ✅ OK - types match

    [Import(typeof(FileLogger))]
    public ILogger Logger2 { get; set; }  // ✅ OK - FileLogger is assignable to ILogger
}
```

### Using object as a catch-all

```cs
using System.ComponentModel.Composition;

class Service
{
    [Import(typeof(ILogger))]
    public object Logger { get; set; }  // ✅ OK - anything is assignable to object
}
```

### Lazy with compatible types

```cs
using System;
using System.ComponentModel.Composition;

class Service
{
    [Import(typeof(ILogger))]
    public Lazy<ILogger> Logger { get; set; }  // ✅ OK
}
```

## How to fix violations

### Option 1: Fix the member type to match the contract

```cs
[Import(typeof(ILogger))]
public ILogger Logger { get; set; }  // Change type to match contract
```

### Option 2: Fix the contract type to match the member

```cs
[Import(typeof(IDatabase))]
public IDatabase Database { get; set; }  // Change contract to match member type
```

### Option 3: Remove explicit contract type

```cs
[Import]
public ILogger Logger { get; set; }  // Let MEF infer the contract type
```

## When to suppress errors

This error should generally not be suppressed as it indicates a bug that will cause runtime failures. If you're doing something unusual with MEF that the analyzer doesn't understand, ensure you have tests that verify the composition works correctly.

## Notes

This analyzer only applies to MEFv1 (`System.ComponentModel.Composition`) because MEFv2 (`System.Composition`) does not support explicit contract types in its Import attribute.
