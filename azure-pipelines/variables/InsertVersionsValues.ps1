[string]::join(',',(@{
    ('MicrosoftVisualStudioCompositionVersion') = & { (dotnet tool run nbgv get-version --project "$PSScriptRoot\..\..\src\Microsoft.VisualStudio.Composition" --format json | ConvertFrom-Json).AssemblyVersion };
}.GetEnumerator() |% { "$($_.key)=$($_.value)" }))
