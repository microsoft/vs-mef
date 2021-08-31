$nbgv = & "$PSScriptRoot\..\Get-nbgv.ps1"
[string]::join(',',(@{
    ('MicrosoftVisualStudioCompositionVersion') = & { (& $nbgv get-version --project "$PSScriptRoot\..\..\src\Microsoft.VisualStudio.Composition" --format json | ConvertFrom-Json).AssemblyVersion };
}.GetEnumerator() |% { "$($_.key)=$($_.value)" }))
