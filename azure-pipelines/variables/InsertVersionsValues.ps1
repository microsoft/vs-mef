$MacroName = 'MicrosoftVisualStudioCompositionVersion'
$SampleProject = "$PSScriptRoot\..\..\src\Microsoft.VisualStudio.Composition"
[string]::join(',',(@{
    ($MacroName) = & { (dotnet nbgv get-version --project $SampleProject --format json | ConvertFrom-Json).AssemblyVersion };
}.GetEnumerator() |% { "$($_.key)=$($_.value)" }))
