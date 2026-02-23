# VSMEF009 ImportMany on non-collection type

The `[ImportMany]` attribute must be applied to a member whose type is a collection type.

## Cause

A property, field, or constructor parameter is decorated with `[ImportMany]` but its type is not a collection, or the collection type is not supported for the member kind.

## Rule description

`[ImportMany]` is used to import all exports matching a contract into a collection. The supported collection types depend on:

1. **Whether it's a property/field or constructor parameter**
2. **Whether the property has a setter** (for properties without setters, the collection must be pre-initialized)

This analyzer enforces consistent validation across both MEFv1 and MEFv2 attribute libraries, even though the runtime behavior differs (MEFv1 throws an exception, MEFv2 silently ignores the import).

### Supported collection types

| Collection Type | Property/field with setter | Property/field without setter (pre-initialized) | Constructor parameter |
|----------------|---------------------|-------------------------------------------|----------------------|
| `T[]` (array) | ✅ | ❌ (arrays don't support `Add()`) | ✅ |
| `IEnumerable<T>` | ✅ | ✅ (must be initialized with a concrete collection) | ✅ |
| `ICollection<T>` | ✅ | ✅ (must be initialized with a concrete collection) | ❌ |
| `IList<T>` | ✅ | ✅ (must be initialized with a concrete collection) | ❌ |
| `List<T>` | ✅ | ✅ | ❌ |
| `HashSet<T>` | ✅ | ✅ | ❌ |
| Custom `ICollection<T>` with public default ctor | ✅ | ✅ | ❌ |

### Constructor parameter restrictions

Constructor parameters have stricter requirements because VS-MEF must create the collection value to pass to the constructor. Only these types are supported:

- `T[]` (array)
- `IEnumerable<T>` (VS-MEF creates a `List<T>`)

Other collection types like `ICollection<T>`, `IList<T>`, `List<T>`, etc. are not supported for constructor parameters.

### Property/field without setter (pre-initialized collection)

VS-MEF supports `[ImportMany]` on properties/fields without a setter, provided:

1. The property is pre-initialized (in the constructor or via field initializer) with a non-null collection instance
2. The collection type implements `ICollection<T>` (so it has `Clear()` and `Add()` methods)

When VS-MEF encounters a pre-initialized collection:

1. It reads the existing property value
2. If non-null, it calls `Clear()` on the existing collection, then `Add()` for each exported value
3. **It does NOT call the property setter** - the original collection instance is reused

Arrays cannot be used with this pattern because they don't support `Add()`. Interface-typed properties (`IEnumerable<T>`, `ICollection<T>`, `IList<T>`) work if initialized with a concrete collection (e.g., `public IList<T> Items { get; } = new List<T>();`).

If the property value is null at runtime, VS-MEF will attempt to create a new collection and set the property, which will fail if there's no setter.

#### Analyzer detection of pre-initialization

The analyzer uses the following heuristics to determine if a setter-less property is properly initialized:

1. **Property initializer detected**: If the property has an initializer (e.g., `= new List<T>();`), no diagnostic is reported.
2. **Constructor assignment detected**: If the property is assigned in all non-chaining constructors (those that don't call `this(...)`), no diagnostic is reported.
3. **Otherwise**: A diagnostic is reported, since the analyzer cannot verify the property will be non-null at runtime.

Note: Complex control flow (if/else, try/catch) within constructors may result in false positives. In such cases, prefer using a property initializer or suppress the diagnostic.

### Lazy and ImportMany

When using `[ImportMany]` with `Lazy<T>`, the collection must contain the lazy wrapper, not wrap the collection:

- ✅ `IEnumerable<Lazy<T>>` - Correct: collection of lazy items
- ❌ `Lazy<IEnumerable<T>>` - Invalid: `Lazy<T>` is not a collection type

If `[ImportMany]` is applied to an unsupported type or configuration, VS-MEF will fail. The specific failure mode depends on the MEF attribute library:

- **MEFv1**: Throws an exception during part discovery or composition
- **MEFv2**: Silently ignores the import (property/field will not be populated)

This analyzer reports diagnostics consistently for both cases, since neither outcome is desirable.

## Examples of violations

### ImportMany on a single value type

```cs
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public ILogger Logger { get; set; }  // ❌ ILogger is not a collection
}
```

### ImportMany on primitive type

```cs
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public string Name { get; set; }  // ❌ string is not a collection type
}
```

### ImportMany on Lazy without collection

```cs
using System;
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public Lazy<ILogger> Logger { get; set; }  // ❌ Should be IEnumerable<Lazy<ILogger>>
}
```

### ImportMany on Lazy wrapping a collection (wrong order)

```cs
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public Lazy<IEnumerable<ILogger>> Loggers { get; set; }  // ❌ Lazy<T> is not a collection; use IEnumerable<Lazy<ILogger>> instead
}
```

### ImportMany on property without setter and no pre-initialization

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportMany]
    public List<ILogger> Loggers { get; }  // ❌ No setter and not pre-initialized - will fail at runtime
}
```

### ImportMany on array without setter

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportMany]
    public ILogger[] Loggers { get; }  // ❌ Arrays cannot be pre-initialized for ImportMany (no Add method)
}
```

### ImportMany with unsupported constructor parameter type

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] List<ILogger> loggers)  // ❌ List<T> not supported in constructor; use T[] or IEnumerable<T>
    {
    }
}
```

## Valid scenarios (no diagnostic)

### Array type with setter

```cs
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public ILogger[] Loggers { get; set; }  // ✅ OK
}
```

### IEnumerable with setter

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public IEnumerable<ILogger> Loggers { get; set; }  // ✅ OK
}
```

### List with setter

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public List<ILogger> Loggers { get; set; }  // ✅ OK
}
```

### Collection of Lazy with setter

```cs
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public IEnumerable<Lazy<ILogger>> Loggers { get; set; }  // ✅ OK
}
```

### Pre-initialized collection without setter

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    public Service()
    {
        this.Loggers = new List<ILogger>();
    }

    [ImportMany]
    public List<ILogger> Loggers { get; }  // ✅ OK - VS-MEF will Clear() then Add() to existing collection
}
```

### Pre-initialized interface-typed collection without setter

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportMany]
    public IList<ILogger> Loggers { get; } = new List<ILogger>();  // ✅ OK - initialized with concrete type
}
```

### Pre-initialized custom collection without setter

```cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportMany]
    public ObservableCollection<ILogger> Loggers { get; } = new ObservableCollection<ILogger>();  // ✅ OK - initialized inline
}
```

### Constructor parameter with array

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] ILogger[] loggers)  // ✅ OK - arrays supported in constructor
    {
        this.Loggers = loggers;
    }

    public ILogger[] Loggers { get; }
}
```

### Constructor parameter with IEnumerable

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] IEnumerable<ILogger> loggers)  // ✅ OK - IEnumerable<T> supported in constructor
    {
        this.Loggers = loggers.ToList();
    }

    public List<ILogger> Loggers { get; }
}
```

## How to fix violations

### Option 1: Change to a collection type

```cs
// Before
[ImportMany]
public ILogger Logger { get; set; }

// After
[ImportMany]
public IEnumerable<ILogger> Loggers { get; set; }
```

### Option 2: Use Import instead

If you only need a single export, use `[Import]` instead:

```cs
// Before
[ImportMany]
public ILogger Logger { get; set; }

// After
[Import]
public ILogger Logger { get; set; }
```

### Option 3: Add a setter to the property

```cs
// Before
[ImportMany]
public List<ILogger> Loggers { get; }

// After
[ImportMany]
public List<ILogger> Loggers { get; set; }
```

### Option 4: Pre-initialize the collection

If you want to keep the property read-only:

```cs
// Before - fails at runtime
[ImportMany]
public List<ILogger> Loggers { get; }

// After
[ImportMany]
public List<ILogger> Loggers { get; } = new List<ILogger>();
```

## When to suppress warnings

You may suppress this warning if:

- The property is assigned in the constructor via complex control flow that the analyzer cannot detect
- You are certain the property will be initialized before MEF composition occurs

In general, prefer using a property initializer (`= new List<T>();`) which is both clearer and easier for the analyzer to verify.

## Notes

- **Runtime behavior difference**: With MEFv1 attributes, invalid `[ImportMany]` configurations throw exceptions. With MEFv2 attributes, `[ImportMany]` on properties without setters is silently ignored. This analyzer reports diagnostics for both cases.
- **Pre-initialized collections**: When VS-MEF finds a non-null collection value, it reuses the existing instance by calling `Clear()` then `Add()` for each export. The property setter is never called in this case.
- **Constructor parameters**: Only `T[]` and `IEnumerable<T>` are supported. VS-MEF creates the collection instance to pass to the constructor.
- **Analyzer limitations**: The analyzer detects property initializers and simple constructor assignments. Complex control flow (conditionals, loops, try/catch) within constructors may not be fully analyzed, potentially resulting in false positives. Chained constructor calls (`this(...)`) are followed when analyzing assignments.
- Interface-typed properties (`IEnumerable<T>`, `ICollection<T>`, `IList<T>`) can be used with the pre-initialized pattern if initialized with a concrete collection that implements `ICollection<T>`.
- Arrays cannot be used with the pre-initialized pattern because they don't support the `Add()` method.
- `Lazy<IEnumerable<T>>` is **not** valid for `[ImportMany]`. Use `IEnumerable<Lazy<T>>` instead.
