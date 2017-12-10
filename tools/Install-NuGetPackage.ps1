<#
.SYNOPSIS
    Installs a NuGet package.
.PARAMETER PackageID
    The Package ID to install.
.PARAMETER Version
    The version of the package to install. If unspecified, the latest stable release is installed.
.PARAMETER Source
    The package source feed to find the package to install from.
.PARAMETER PackagesDir
    The directory to install the package to. By default, it uses the Packages folder at the root of the repo.
#>
Param(
    [Parameter(Position=1,Mandatory=$true)]
    [string]$PackageId,
    [Parameter()]
    [string]$Version,
    [Parameter()]
    [string]$Source,
    [Parameter()]
    [string[]]$FallbackSources,
    [Parameter()]
    [switch]$Prerelease,
    [Parameter()]
    [string]$PackagesDir="$PSScriptRoot\..\packages",
    [Parameter()]
    [ValidateSet('Quiet','Normal','Detailed')]
    [string]$Verbosity='normal'
)

$nugetPath = & "$PSScriptRoot\Get-NuGetTool.ps1"

if (!(Test-Path $PackagesDir)) {
    $null = md $PackagesDir
}

Push-Location $PackagesDir
try {
    Write-Verbose "Installing $PackageId..."
    $nugetArgs = "Install",$PackageId
    if ($Version) { $nugetArgs += "-Version",$Version }
    if ($Source) { $nugetArgs += "-Source",$Source }
    if ($FallbackSources) { $FallbackSources |% { $nugetArgs += "-FallbackSource",$_ } }
    if ($Prerelease) { $nugetArgs += "-Prerelease" }
    $nugetArgs += '-Verbosity',$Verbosity

    Write-Host "`"$nugetPath`" $nugetArgs" -ForegroundColor Gray
    $p = Start-Process -FilePath $nugetPath -ArgumentList $nugetArgs -NoNewWindow -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw }
} finally {
    Pop-Location
}
