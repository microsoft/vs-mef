# VSMEF010 ImportMany with unsupported collection type in constructor

When using `[ImportMany]` on a constructor parameter, only `T[]` (arrays) and `IEnumerable<T>` are supported as collection types.

## Cause

A constructor parameter decorated with `[ImportMany]` uses a collection type other than array or `IEnumerable<T>`. While properties support additional collection types like `List<T>`, `ICollection<T>`, and custom collections, constructor parameters have stricter requirements.

## Rule description

MEF needs to create the collection instance before passing it to the constructor. For constructor parameters, MEF only supports:

- **Arrays (`T[]`)**: MEF creates a new array
- **`IEnumerable<T>`**: MEF creates an array and passes it (arrays implement `IEnumerable<T>`)

Other collection types like `List<T>`, `IList<T>`, `ICollection<T>`, or custom collection types are not supported for constructor parameters because MEF cannot instantiate them appropriately.

For properties, MEF has more flexibility because it can either:

- Create the collection and assign it to the property
- Add items to an existing collection that was pre-initialized in the constructor

## Examples of violations

### List in constructor

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] List<ILogger> loggers)  // ❌ List<T> not supported
    {
    }
}
```

### IList in constructor

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] IList<ILogger> loggers)  // ❌ IList<T> not supported
    {
    }
}
```

### ICollection in constructor

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] ICollection<ILogger> loggers)  // ❌ ICollection<T> not supported
    {
    }
}
```

### HashSet in constructor

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] HashSet<ILogger> loggers)  // ❌ HashSet<T> not supported
    {
    }
}
```

## Valid scenarios (no diagnostic)

### Array in constructor

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] ILogger[] loggers)  // ✅ OK
    {
    }
}
```

### IEnumerable in constructor

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] IEnumerable<ILogger> loggers)  // ✅ OK
    {
    }
}
```

### List on property (not constructor)

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportMany]
    public List<ILogger> Loggers { get; set; }  // ✅ OK for properties
}
```

### Array of Lazy in constructor

```cs
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] Lazy<ILogger>[] loggers)  // ✅ OK
    {
    }
}
```

### IEnumerable of ExportFactory in constructor

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service([ImportMany] IEnumerable<ExportFactory<ILogger>> loggerFactories)  // ✅ OK
    {
    }
}
```

## How to fix violations

### Option 1: Change to array type

```cs
// Before
[ImportingConstructor]
public Service([ImportMany] List<ILogger> loggers)

// After
[ImportingConstructor]
public Service([ImportMany] ILogger[] loggers)
```

### Option 2: Change to IEnumerable

```cs
// Before
[ImportingConstructor]
public Service([ImportMany] List<ILogger> loggers)

// After
[ImportingConstructor]
public Service([ImportMany] IEnumerable<ILogger> loggers)
```

### Option 3: Convert to List inside the constructor

If you need a `List<T>` for mutation, create it from the IEnumerable:

```cs
[Export]
class Service
{
    private readonly List<ILogger> loggers;

    [ImportingConstructor]
    public Service([ImportMany] IEnumerable<ILogger> loggers)
    {
        this.loggers = loggers.ToList();
    }
}
```

### Option 4: Move to property import

If you need a specific collection type, use a property import instead:

```cs
[Export]
class Service
{
    [ImportMany]
    public List<ILogger> Loggers { get; set; }  // Properties support more collection types
}
```

## When to suppress warnings

This warning should not be suppressed as it indicates code that will fail at runtime during composition.
