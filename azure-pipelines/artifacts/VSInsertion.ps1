# This artifact captures everything needed to insert into VS (NuGet packages, insertion metadata, etc.)

$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$PackagesRoot = "$RepoRoot/bin/Packages/$BuildConfiguration/NuGet"

if (!(Test-Path $PackagesRoot))  { return @{} }

@{
    "$PackagesRoot" = (Get-ChildItem $PackagesRoot -Recurse -Exclude 'Microsoft.VisualStudio.Composition.AppHost.*')
}
