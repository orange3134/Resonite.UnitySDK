param([Parameter(Mandatory)][string]$PythonPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'setup-windows.ps1')
function Assert($Condition,[string]$Message) {if (-not $Condition) {throw $Message}}

$value='space & 日本語 "quoted" trailing\'
$json=Invoke-SetupProcess $PythonPath @('-I','-c','import sys,json;print(json.dumps(sys.argv[1:]))',$value,'') 20
Assert ($json.Code -eq 0) 'Python exit code survives Windows PowerShell 5.1 WaitForExit.'
$actual=$json.Text | ConvertFrom-Json
Assert ($actual[0] -ceq $value -and $actual[1] -ceq '') 'Native argument boundaries/quotes/Unicode/empty arguments survive.'
$failure=Invoke-SetupProcess $PythonPath @('-I','-c','raise SystemExit(7)') 20
Assert ($failure.Code -eq 7) 'Nonzero child status must not become a false success.'
Assert ([bool](Test-SetupPython $PythonPath)) 'Existing Python is reusable.'
Assert (-not (Test-SetupPython 'C:\missing-python.exe')) 'Missing Python is rejected.'

& {
    function Get-VerifiedInstaller {throw 'Should not download with invalid metadata'}
    $rejected=$false
    try {Install-SetupDependency 'Unity' '2022.3.62f3 /evil' 'bad'} catch {$rejected=$_.ToString() -like '*Invalid project*'}
    Assert $rejected 'Malformed Unity metadata must fail before any download/launch.'
}
& {
    $script:seen=$null
    function Get-VerifiedInstaller {param($Url,$Name,$Publisher)
        Assert ($Url -eq 'https://download.unity3d.com/download_unity/96770f904ca7/Windows64EditorInstaller/UnitySetup64-2022.3.62f3.exe') 'Pinned official installer URL'
        return 'C:\fake installer.exe'
    }
    function Start-Process {param($FilePath,$ArgumentList,$WindowStyle,[switch]$Wait,[switch]$PassThru)
        $script:seen=$ArgumentList
        [pscustomobject]@{ExitCode=0}
    }
    Install-SetupDependency 'Unity' '2022.3.62f3' '96770f904ca7'
    Assert ($script:seen -eq ('/S /D='+(Join-Path $env:LOCALAPPDATA 'OpenBrushImporter/Unity/2022.3.62f3'))) 'NSIS /D must be last and unquoted.'
}
& {
    function Get-VerifiedInstaller {return 'C:\fake installer.exe'}
    function Start-Process {param($FilePath,$ArgumentList,$WindowStyle,[switch]$Wait,[switch]$PassThru) [pscustomobject]@{ExitCode=1603}}
    $rejected=$false
    try {Install-SetupDependency 'Python' '' ''} catch {$rejected=$_.ToString() -like '*failed*1603*'}
    Assert $rejected 'Installer failure must stop setup.'
}
& {
    $folder=Join-Path $env:TEMP ('openbrush-signature-test-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $folder | Out-Null
    $priorLocal=$env:LOCALAPPDATA
    try {
        $env:LOCALAPPDATA=$folder
        $cache=Join-Path $folder 'OpenBrushImporter/Downloads'
        New-Item -ItemType Directory -Path $cache -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $cache 'invalid.exe') -Value 'not executable'
        function Get-AuthenticodeSignature {[pscustomobject]@{Status='NotSigned'; SignerCertificate=$null}}
        $rejected=$false
        try {Get-VerifiedInstaller 'https://www.python.org/not-used' 'invalid.exe' 'Python Software Foundation'} catch {$rejected=$_.ToString() -like '*signature verification failed*'}
        Assert $rejected 'Unsigned cached download must never execute.'
    } finally {
        $env:LOCALAPPDATA=$priorLocal
        # Explicit fixed test descendants only; no recursive removal.
        Remove-Item -LiteralPath (Join-Path $cache 'invalid.exe') -ErrorAction SilentlyContinue
        [IO.Directory]::Delete($cache); [IO.Directory]::Delete((Split-Path $cache)); [IO.Directory]::Delete($folder)
    }
}
Write-Output 'WINDOWS_SETUP_CHECKS_PASSED'
