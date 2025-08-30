# VSMEF003: Exported type not implemented by exporting class

A class that declares `[Export(typeof(T))]` should implement interface `T` or inherit from class `T`.

## Cause

A class is decorated with `[Export(typeof(T))]` but does not implement the interface or inherit from the class specified by `T`.

## Rule description

When using MEF Export attributes with an explicit type parameter, the exporting class should implement that type. If the class does not implement the specified interface or inherit from the specified base class, it will likely cause runtime composition failures or unexpected behavior.

## How to fix violations

Either:

- Make the exporting class implement the specified interface, or
- Make the exporting class inherit from the specified base class, or
- Change the Export attribute to export the correct type, or
- Remove the type parameter to export the class's own type

## When to suppress warnings

This warning can be suppressed if you intentionally want to export a type that is not implemented by the exporting class, though this is rarely a good practice and may cause composition issues at runtime.

## Example

### Violates

[!code-csharp[](../../samples/AnalyzerDocs/VSMEF003.cs#Defective)]

### Does not violate

[!code-csharp[](../../samples/AnalyzerDocs/VSMEF003.cs#Fix)]
