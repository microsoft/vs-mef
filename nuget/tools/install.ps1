param($installPath, $toolsPath, $package, $project)

# This is the MSBuild targets file to add
$targetsFile = [System.IO.Path]::Combine($toolsPath, 'Microsoft.VisualStudio.Composition.targets')

# Make the path to the targets file relative.
$projectUri = new-object Uri('file://' + $project.FullName)
$targetUri = new-object Uri('file://' + $targetsFile)
$relativePath = $projectUri.MakeRelativeUri($targetUri).ToString().Replace([System.IO.Path]::AltDirectorySeparatorChar, [System.IO.Path]::DirectorySeparatorChar)

# Need to load MSBuild assembly if it’s not loaded yet.
Add-Type -AssemblyName ‘Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a’

# Grab the loaded MSBuild project for the project
$msbuild = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName) | Select-Object -First 1

# Add the import and save the project
$msbuild.Xml.AddImport($relativePath) | out-null

# Using a 'supported' wizard, but causes issues with the NuGet package installer.
# Add-Type -AssemblyName 'Microsoft.VisualStudio.ProjectSystem.VS.Implementation, Version=12.0.0.0, Culture=Neutral, PublicKeyToken=B03F5F7F11D50A3A'
# Add-Type -AssemblyName 'Microsoft.VisualStudio.TemplateWizardInterface, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
# 
# $wizard = New-Object Microsoft.VisualStudio.ProjectSystem.VS.Implementation.Package.AddImportWizard
# 
# $replaceParameters = New-Object 'System.Collections.Generic.Dictionary[String,String]'
# $replaceParameters.Add("AddImportRelativeTo", "targets")
# $replaceParameters.Add("AddImportRelativePosition", "After")
# $replaceParameters.Add("AddImportFile", "$relativePath")
# $replaceParameters.Add("AddImportCondition", "")
# 
# $wizard.RunStarted($null, $replaceParameters, [Microsoft.VisualStudio.TemplateWizard.WizardRunKind]::AsNewItem, $null)
# $wizard.GetType().GetMethod("ProjectFinishedGenerating").Invoke($wizard, @($project))
# $wizard.RunFinished()
