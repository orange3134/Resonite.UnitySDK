param([Parameter(Mandatory)][ValidateSet('Unity','Hub','Git')][string]$Kind,[string]$Version,[string]$Changeset)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'setup-windows.ps1')
try {Install-SetupDependency $Kind $Version $Changeset} catch {Write-Host $_ -ForegroundColor Red; exit 1}
