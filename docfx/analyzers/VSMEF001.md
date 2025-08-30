# VSMEF001 Importing property must have setter

A property with an `[ImportAttribute]` must define a setter so the export can be set on the property.

The following property definition would produce a diagnostic from this rule:

[!code-csharp[](../../samples/AnalyzerDocs/VSMEF001.cs#Defective)]

Such a property does not offer a setter and thus MEF would be unable to set the property with a value.

Fix the diagnostic by adding a setter:

[!code-csharp[](../../samples/AnalyzerDocs/VSMEF001.cs#Fix)]
