# Windows PowerShell 5.1 compatible. Dot-sourcing this file does not install anything.
function ConvertTo-NativeArgument([string]$Value) {
    # CommandLineToArgvW/CRT quoting; no cmd.exe interpolation.
    '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

function Invoke-SetupProcess([string]$File, [string[]]$Arguments, [int]$TimeoutSeconds=60) {
    $id=[guid]::NewGuid().ToString('N')
    $out=Join-Path $env:TEMP "openbrush-$id.out"
    $err=Join-Path $env:TEMP "openbrush-$id.err"
    $p=$null
    try {
        $native=($Arguments | ForEach-Object { ConvertTo-NativeArgument $_ }) -join ' '
        $p=Start-Process -FilePath $File -ArgumentList $native -WindowStyle Hidden -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
        $null=$p.Handle # Pin the handle: Windows PowerShell 5.1 otherwise loses ExitCode.
        if (-not $p.WaitForExit($TimeoutSeconds*1000)) {
            # This exact process was created here; never kill by executable name.
            $p.Kill(); $p.WaitForExit(); throw "Timed out: $File"
        }
        $p.WaitForExit() # Flush redirected streams after the timed wait.
        [pscustomobject]@{Code=$p.ExitCode; Text=(Get-Content -LiteralPath $out -Raw -Encoding UTF8 -ErrorAction SilentlyContinue); Error=(Get-Content -LiteralPath $err -Raw -Encoding UTF8 -ErrorAction SilentlyContinue)}
    } finally {
        if ($p) {$p.Dispose()}
        Remove-Item -LiteralPath $out,$err -Force -ErrorAction SilentlyContinue
    }
}

function Test-SetupPython([string]$Path) {
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {return $null}
    # Never execute Windows Store alias stubs during discovery.
    if ($Path -like '*\Microsoft\WindowsApps\python*.exe') {return $null}
    try {
        $code='import sys,struct,json,tkinter,venv; assert sys.implementation.name=="cpython" and (3,10)<=sys.version_info[:2]<(3,15) and struct.calcsize("P")==8; r=tkinter.Tk(); r.withdraw(); r.destroy(); print(json.dumps(sys.executable))'
        $r=Invoke-SetupProcess $Path @('-I','-c',$code) 20
        if ($r.Code -eq 0) {return ($r.Text | ConvertFrom-Json)}
    } catch {}
    return $null
}

function Find-SetupPython([string]$Sdk, [string]$ExplicitPath) {
    if ($ExplicitPath) {
        $found=Test-SetupPython $ExplicitPath
        if (-not $found) {throw 'Specified Python needs CPython 3.10-3.14, 64-bit, Tcl/Tk and venv.'}
        return $found
    }
    $paths=@((Join-Path $Sdk '.venv-openbrush/Scripts/python.exe'),(Join-Path $env:LOCALAPPDATA 'OpenBrushImporter/Python313/python.exe'))
    $launcher=Get-Command py.exe -ErrorAction SilentlyContinue
    if ($launcher) {
        try {
            $r=Invoke-SetupProcess $launcher.Source @('-0p') 20
            foreach ($line in ($r.Text -split "`n")) {
                if ($line -match '([A-Za-z]:\\.*python(?:3)?\.exe)\s*$') {$paths+=$matches[1]}
            }
        } catch {}
    }
    foreach ($base in @('HKCU:\Software\Python\PythonCore','HKLM:\Software\Python\PythonCore','HKLM:\Software\WOW6432Node\Python\PythonCore')) {
        foreach ($key in (Get-ChildItem $base -ErrorAction SilentlyContinue)) {
            $install=Get-Item (Join-Path $key.PSPath 'InstallPath') -ErrorAction SilentlyContinue
            if ($install) {
                $paths+=$install.GetValue('ExecutablePath')
                if ($install.GetValue('')) {$paths+=Join-Path $install.GetValue('') 'python.exe'}
            }
        }
    }
    foreach ($name in @('python.exe','python3.exe')) {
        $command=Get-Command $name -ErrorAction SilentlyContinue
        if ($command) {$paths+=$command.Source}
    }
    $paths+=Get-ChildItem "$env:LOCALAPPDATA\Programs\Python\Python*\python.exe" -ErrorAction SilentlyContinue | ForEach-Object FullName
    foreach ($path in ($paths | Where-Object {$_} | Select-Object -Unique)) {
        $found=Test-SetupPython $path
        if ($found) {return $found}
    }
    return $null
}

function Get-VerifiedInstaller([string]$Url,[string]$Name,[string]$Publisher) {
    $folder=Join-Path $env:LOCALAPPDATA 'OpenBrushImporter/Downloads'
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $path=Join-Path $folder $Name
    if (-not (Test-Path -LiteralPath $path)) {
        $part="$path.$([guid]::NewGuid().ToString('N')).part"
        Write-Host "Downloading $Name ..."
        [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
        $priorProgress=$ProgressPreference
        try {
            # Windows PowerShell 5.1 progress rendering slows multi-GB downloads.
            # The caller reports the phase and elapsed time while this runs.
            $ProgressPreference='SilentlyContinue'
            Invoke-WebRequest -Uri $Url -OutFile $part -UseBasicParsing
            Move-Item -LiteralPath $part -Destination $path
        } finally {$ProgressPreference=$priorProgress; Remove-Item -LiteralPath $part -Force -ErrorAction SilentlyContinue}
    }
    $signature=Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch $Publisher) {
        throw "Installer signature verification failed. Remove this cached file and retry: $path"
    }
    return $path
}

function Install-SetupDependency([string]$Kind,[string]$Version,[string]$Changeset) {
    $homeDir=Join-Path $env:LOCALAPPDATA 'OpenBrushImporter'
    switch ($Kind) {
        'Python' {
            $file=Get-VerifiedInstaller 'https://www.python.org/ftp/python/3.13.17/python-3.13.17-amd64.exe' 'python-3.13.17-amd64.exe' 'Python Software Foundation'
            $target=Join-Path $homeDir 'Python313'
            $arguments='/quiet InstallAllUsers=0 Include_launcher=0 Include_test=0 Include_pip=1 Include_tcltk=1 AssociateFiles=0 Shortcuts=0 PrependPath=0 TargetDir="'+$target+'"'
        }
        'Unity' {
            if ($Version -notmatch '^\d+\.\d+\.\d+[fpab]\d+$' -or $Changeset -notmatch '^[a-f0-9]{12}$') {throw 'Invalid project Unity version/changeset.'}
            $file=Get-VerifiedInstaller "https://download.unity3d.com/download_unity/$Changeset/Windows64EditorInstaller/UnitySetup64-$Version.exe" "UnitySetup64-$Version.exe" 'Unity (Technologies|Software)'
            $target=Join-Path $homeDir "Unity/$Version"
            # NSIS requires /D to be last and UNQUOTED, including paths with spaces.
            $arguments='/S /D='+$target
        }
        'Hub' {
            $file=Get-VerifiedInstaller 'https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe' 'UnityHubSetup.exe' 'Unity (Technologies|Software)'
            $arguments='/S'
        }
        'Git' {
            $winget=Get-Command winget.exe -ErrorAction SilentlyContinue
            if (-not $winget) {throw 'Git is missing. Install Git for Windows (https://git-scm.com/download/win), then run Start-OpenBrush-Importer.cmd again.'}
            $file=$winget.Source
            $arguments='install --id Git.Git --exact --source winget --silent --accept-source-agreements --accept-package-agreements --disable-interactivity'
        }
        default {throw "Unsupported dependency: $Kind"}
    }
    Write-Host "Installing $Kind ... Windows may ask for administrator approval."
    $process=Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -eq 3010) {throw "$Kind installed. Restart Windows, then run Start-OpenBrush-Importer.cmd again."}
    if ($process.ExitCode -ne 0) {throw "$Kind installer failed (exit $($process.ExitCode)). Run Start-OpenBrush-Importer.cmd again after resolving the installer error."}
}
