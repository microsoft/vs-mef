# VSMEF004 Exported type missing importing constructor

A class that exports itself or has exported members must be instantiable by MEF. If the class defines non-default constructors, it must have either:

1. A parameterless constructor, or
2. A constructor annotated with `[ImportingConstructor]`

The following class definition would produce a diagnostic from this rule:

```cs
[Export]
class Foo
{
    public Foo(string parameter)
    {
        // Non-default constructor without [ImportingConstructor]
    }
}
```

This class exports itself but only has a non-default constructor that is not annotated with `[ImportingConstructor]`. MEF cannot instantiate this class because it doesn't know how to provide the required `string` parameter.

## Fixing the diagnostic

There are several ways to fix this diagnostic:

### Option 1: Add a parameterless constructor

```cs
[Export]
class Foo
{
    public Foo()
    {
        // Default constructor for MEF
    }

    public Foo(string parameter)
    {
        // Non-default constructor for other uses
    }
}
```

### Option 2: Annotate a constructor with [ImportingConstructor]

```cs
[Export]
class Foo
{
    [ImportingConstructor]
    public Foo(string parameter)
    {
        // MEF will satisfy the 'parameter' import
    }
}
```

### Option 3: Use importing constructor with MEF imports

```cs
[Export]
class Foo
{
    [ImportingConstructor]
    public Foo([Import] IService service)
    {
        // MEF will inject the required IService
    }
}
```

### Option 4: Add [PartNotDiscoverable] for manually constructed parts

If your part is intended to be manually constructed and inserted into the composition (e.g., for recomposition or mocking scenarios), you can mark it with `[PartNotDiscoverable]`:

```cs
[Export]
[PartNotDiscoverable]
class Foo
{
    public Foo(string parameter)
    {
        // This part will be manually constructed and composed
    }
}
```

This tells MEF not to discover and instantiate this part automatically. You must then manually create instances and add them to the composition container.

## When to suppress warnings

You can suppress this diagnostic if:

1. **Manual construction scenarios**: Your part is intentionally constructed outside of MEF and inserted into the composition using methods like `CompositionContainer.ComposeParts()` or `CompositionContainer.SatisfyImportsOnce()`. Consider using `[PartNotDiscoverable]` instead of suppressing, as it makes the intent explicit.

2. **Mocking/testing scenarios**: The type is used for testing purposes where you manually construct mock instances. Again, `[PartNotDiscoverable]` may be a better choice.

3. **Very advanced recomposition scenarios**: You're using MEF v1's recomposition feature where parts can be dynamically added to or removed from the composition at runtime.

## Examples that trigger this rule

### Class-level export with non-default constructor

```cs
[Export]
class Service
{
    public Service(string connectionString)  // ❌ Error: missing [ImportingConstructor]
    {
    }
}
```

### Class with exported members and non-default constructor

```cs
class ServiceProvider
{
    [Export]
    public IService GetService() => new Service();

    public ServiceProvider(string config)  // ❌ Error: missing [ImportingConstructor]
    {
    }
}
```

### Mixed static and instance exports

```cs
class MixedExports
{
    [Export]
    public static string StaticValue = "test";  // ✅ OK: static export

    [Export]
    public string InstanceValue { get; set; }  // ❌ Requires instantiation

    public MixedExports(string parameter)  // ❌ Error: missing [ImportingConstructor]
    {
    }
}
```

## Examples that do NOT trigger this rule

### Class with only static exports

```cs
class StaticOnlyExports
{
    [Export]
    public static IService Service { get; } = new Service();

    public StaticOnlyExports(string parameter)  // ✅ OK: no instance exports
    {
    }
}
```

### Class with default constructor

```cs
[Export]
class Service
{
    public Service()  // ✅ OK: default constructor
    {
    }
}
```

### Class with importing constructor

```cs
[Export]
class Service
{
    [ImportingConstructor]
    public Service(string connectionString)  // ✅ OK: has [ImportingConstructor]
    {
    }
}
```

### Abstract class

```cs
[Export]
abstract class ServiceBase
{
    public ServiceBase(string parameter)  // ✅ OK: abstract classes are not instantiated
    {
    }
}
```

## Applies to both MEF versions

This rule applies to both MEF v1 (`System.ComponentModel.Composition`) and MEF v2 (`System.Composition`) attributes:

```cs
// MEF v1
[System.ComponentModel.Composition.Export]
class Service
{
    [System.ComponentModel.Composition.ImportingConstructor]
    public Service(string parameter) { }
}

// MEF v2
[System.Composition.Export]
class Service
{
    [System.Composition.ImportingConstructor]
    public Service(string parameter) { }
}
```
