param([string]$PythonPath,[string]$UnityPath,[string]$BrushAssets,[switch]$NoInstall,[switch]$NoLaunch,[switch]$NonInteractive,[switch]$ForceInitialize)
$ErrorActionPreference='Stop'
$env:PYTHONUTF8='1'
$env:PYTHONUNBUFFERED='1'
$sdk=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $PSScriptRoot 'setup-windows.ps1')
$setupLock=$null
try {
    Write-Host 'Open Brush -> Resonite setup'
    New-Item -ItemType Directory -Path (Join-Path $sdk '.local/importer') -Force | Out-Null
    $setupLock=[IO.File]::Open((Join-Path $sdk '.local/importer/setup.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    Write-Host 'Checking Python (64-bit + Tcl/Tk) ...'
    $python=Find-SetupPython $sdk $PythonPath
    if (-not $python) {
        if ($NoInstall) {throw 'No compatible Python found; automatic installation is disabled.'}
        Install-SetupDependency 'Python' '' ''
        $python=Find-SetupPython $sdk ''
        if (-not $python) {throw 'Python installation did not produce a working 64-bit Tcl/Tk environment.'}
    }
    Write-Host "Using Python: $python"
    $forward=@((Join-Path $PSScriptRoot 'setup.py'))
    if ($UnityPath) {$forward+=@('--unity',$UnityPath)}
    if ($BrushAssets) {$forward+=@('--brush-assets',$BrushAssets)}
    if ($NoInstall) {$forward+='--no-install'}
    if ($NoLaunch) {$forward+='--no-launch'}
    if ($NonInteractive) {$forward+='--non-interactive'}
    if ($ForceInitialize) {$forward+='--force-initialize'}
    & $python @forward
    $result=$LASTEXITCODE
} catch {Write-Host $_ -ForegroundColor Red; $result=1}
finally {if ($setupLock) {$setupLock.Dispose()}}
exit $result
