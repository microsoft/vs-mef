<#
.SYNOPSIS
    Restores NuGet packages.
.PARAMETER Signing
    Install the MicroBuild signing plugin for building test-signed builds on desktop machines.
.PARAMETER Localization
    Install the MicroBuild localization plugin for building loc builds on desktop machines.
    The environment is configured to build pseudo-loc for JPN only, but may be used to build
    all languages with shipping-style loc by using the `/p:loctype=full,loclanguages=vs`
    when building.
.PARAMETER IBCMerge
    Install the MicroBuild IBCMerge plugin for building optimized assemblies on desktop machines.
#>
Param(
    [Parameter()]
    [switch]$Signing,
    [Parameter()]
    [switch]$Localization,
    [Parameter()]
    [switch]$IBCMerge
)

Push-Location $PSScriptRoot
try {
    $HeaderColor = 'Green'
    $toolsPath = "$PSScriptRoot\tools"
    $nugetVerbosity = 'quiet'
    if ($Verbose) { $nugetVerbosity = 'normal' }

    # Restore VS solution dependencies
    dotnet restore src

    $MicroBuildPackageSource = 'https://devdiv.pkgs.visualstudio.com/DefaultCollection/_packaging/MicroBuildToolset/nuget/v3/index.json'
    $VSPackageSource = 'https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json'

    if ($Signing) {
        Write-Host "Installing MicroBuild signing plugin" -ForegroundColor $HeaderColor
        & "$toolsPath\Install-NuGetPackage.ps1" MicroBuild.Plugins.Signing -source $MicroBuildPackageSource -Verbosity $nugetVerbosity
        $env:SignType = "Test"
    }

    if ($Localization) {
        Write-Host "Installing MicroBuild localization plugin" -ForegroundColor $HeaderColor
        & $InstallNuGetPkgScriptPath MicroBuild.Plugins.Localization -source $MicroBuildPackageSource -Verbosity $nugetVerbosity
        $EnvVars['LocType'] = "Pseudo"
        $EnvVars['LocLanguages'] = "JPN"
    }

    if ($IBCMerge) {
        Write-Host "Installing MicroBuild IBCMerge plugin" -ForegroundColor $HeaderColor
        & "$toolsPath\Install-NuGetPackage.ps1" MicroBuild.Plugins.IBCMerge -source $MicroBuildPackageSource -FallbackSources $VSPackageSource -Verbosity $nugetVerbosity
        $env:IBCMergeBranch = "master"
    }

    Write-Host "Successfully restored all dependencies" -ForegroundColor Yellow
}
catch {
    Write-Error "Aborting script due to error"
    exit $lastexitcode
}
finally {
    Pop-Location
}