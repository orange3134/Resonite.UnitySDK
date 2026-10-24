$ErrorActionPreference = 'Stop'
$sdk = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:winexe /reference:System.Windows.Forms.dll "/out:$sdk/OpenBrush Importer.exe" (Join-Path $PSScriptRoot 'Launcher.cs')
if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed' }
Write-Output (Join-Path $sdk 'OpenBrush Importer.exe')
