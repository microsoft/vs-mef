# VSMEF011 Both Import and ImportMany applied to same member

A member cannot have both `[Import]` and `[ImportMany]` attributes. These attributes are mutually exclusive.

## Cause

A property, field, or constructor parameter is decorated with both `[Import]` and `[ImportMany]` attributes. MEF cannot determine whether to import a single value or a collection.

## Rule description

The `[Import]` attribute is used to import a single export matching a contract, while `[ImportMany]` is used to import all exports matching a contract into a collection. Applying both to the same member creates an ambiguous situation that MEF cannot resolve.

## Examples of violations

### Both attributes on a property

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [Import]
    [ImportMany]
    public IEnumerable<ILogger> Loggers { get; set; }  // ❌ Cannot have both
}
```

### Both attributes on a constructor parameter

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

[Export]
class Service
{
    [ImportingConstructor]
    public Service(
        [Import]
        [ImportMany]
        ILogger[] loggers)  // ❌ Cannot have both
    {
    }
}
```

### Both attributes on a field

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [Import]
    [ImportMany]
    private IEnumerable<ILogger> loggers;  // ❌ Cannot have both
}
```

## How to fix violations

### Option 1: Use Import for a single value

```cs
using System.ComponentModel.Composition;

class Service
{
    [Import]
    public ILogger Logger { get; set; }  // ✅ Single import
}
```

### Option 2: Use ImportMany for a collection

```cs
using System.Collections.Generic;
using System.ComponentModel.Composition;

class Service
{
    [ImportMany]
    public IEnumerable<ILogger> Loggers { get; set; }  // ✅ Multiple imports
}
```

## When to suppress warnings

This warning should not be suppressed as it indicates code that will fail during part discovery.
