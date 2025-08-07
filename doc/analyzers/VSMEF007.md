# VSMEF007 Duplicate import contract

A MEF type should not import the same contract more than once. When a type imports the same contract multiple times, it creates ambiguity about which import should receive which export, and may indicate a design problem.

The following class definition would produce a diagnostic from this rule:

```cs
[Export]
class Foo
{
    [Import]
    public string Value1 { get; set; }

    [Import]
    public string Value2 { get; set; }
}
```

This class imports the `string` contract twice through different properties, which means both properties would receive the same exported string value.

## Examples of violations

### Duplicate property imports

```cs
[Export]
class Service
{
    [Import]
    public ILogger Logger1 { get; set; }

    [Import]
    public ILogger Logger2 { get; set; }  // Duplicate import
}
```

### Duplicate constructor parameter imports

```cs
[Export]
class Service
{
    [ImportingConstructor]
    public Service([Import] ILogger logger1, [Import] ILogger logger2)
    {
        // Both parameters would receive the same ILogger instance
    }
}
```

### Mixed property and constructor imports

```cs
[Export]
class Service
{
    [Import]
    public ILogger PropertyLogger { get; set; }

    [ImportingConstructor]
    public Service([Import] ILogger constructorLogger)
    {
        // Both imports target the same contract
    }
}
```

### Duplicate contract names

```cs
[Export]
class Service
{
    [Import("MyContract")]
    public string Value1 { get; set; }

    [Import("MyContract")]
    public string Value2 { get; set; }  // Same contract name
}
```

## Valid scenarios (no diagnostic)

### Different contract types

```cs
[Export]
class Service
{
    [Import]
    public ILogger Logger { get; set; }

    [Import]
    public IConfiguration Configuration { get; set; }  // Different type
}
```

### Different contract names

```cs
[Export]
class Service
{
    [Import("PrimaryLogger")]
    public ILogger Logger1 { get; set; }

    [Import("SecondaryLogger")]
    public ILogger Logger2 { get; set; }  // Different contract name
}
```

### ImportMany for collections

```cs
[Export]
class Service
{
    [ImportMany]
    public IEnumerable<IPlugin> Plugins { get; set; }

    [ImportMany]
    public IEnumerable<IHandler> Handlers { get; set; }  // Different element type
}
```

## Fixing the diagnostic

### Option 1: Use different contract names

If you need multiple instances of the same type, use different contract names:

```cs
[Export]
class Service
{
    [Import("PrimaryDatabase")]
    public IDatabase PrimaryDb { get; set; }

    [Import("SecondaryDatabase")]
    public IDatabase SecondaryDb { get; set; }
}
```

### Option 2: Use ImportMany for collections

If you need all available exports of a contract:

```cs
[Export]
class Service
{
    [ImportMany]
    public IEnumerable<ILogger> Loggers { get; set; }
}
```

### Option 3: Consolidate imports

If you only need one instance, remove the duplicate import:

```cs
[Export]
class Service
{
    [Import]
    public ILogger Logger { get; set; }

    // Use the same logger instance throughout the class
}
```

### Option 4: Use composition pattern

Create a composite that aggregates multiple services:

```cs
public interface ICompositeService
{
    ILogger PrimaryLogger { get; }
    ILogger SecondaryLogger { get; }
}

[Export(typeof(ICompositeService))]
class CompositeService : ICompositeService
{
    [ImportingConstructor]
    public CompositeService(
        [Import("Primary")] ILogger primaryLogger,
        [Import("Secondary")] ILogger secondaryLogger)
    {
        PrimaryLogger = primaryLogger;
        SecondaryLogger = secondaryLogger;
    }

    public ILogger PrimaryLogger { get; }
    public ILogger SecondaryLogger { get; }
}
```

## Benefits

Following this analyzer's recommendations helps ensure:

1. **Clear intent**: Each import has a distinct purpose
2. **Avoiding confusion**: No ambiguity about which export goes where
3. **Better design**: Encourages proper separation of concerns
4. **Performance**: Avoids unnecessary duplicate imports
