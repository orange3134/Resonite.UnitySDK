[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$BundleDirectory,
    [Parameter(Mandatory)][ValidateRange(1,65535)][int]$Port,
    [string]$Name = 'Open Brush sketch',
    [string]$Position = '0,0,2',
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$bundlePath = Join-Path (Resolve-Path -LiteralPath $BundleDirectory).Path 'bundle.json'
if (-not (Test-Path -LiteralPath $bundlePath -PathType Leaf)) { throw "bundle.json がありません: $bundlePath" }
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) { throw "Unity.exe がありません: $Unity" }
if ($Position -notmatch '^-?\d+(\.\d+)?,-?\d+(\.\d+)?,-?\d+(\.\d+)?$') { throw 'Position は 0,0,2 のように指定してください。' }
foreach ($value in @($sdkRoot,$bundlePath,$Name,$Unity)) {
    if ($value.Contains('"') -or $value.Contains("`r") -or $value.Contains("`n")) { throw '引数に引用符や改行は使えません。' }
}
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
$reportPath = Join-Path $sdkRoot ".local\OpenBrush-send-$runId.json"
$logPath = Join-Path $sdkRoot "Logs\OpenBrush-send-$runId.log"
$sendArgs = @(
    '-batchmode'
    '-projectPath', ('"{0}"' -f $sdkRoot)
    '-executeMethod', 'OpenBrushSendModel.Run'
    '-openbrushBundle', ('"{0}"' -f $bundlePath)
    '-openbrushName', ('"{0}"' -f $Name)
    '-openbrushPort', "$Port"
    '-openbrushPosition', $Position
    '-openbrushReport', ('"{0}"' -f $reportPath)
    '-logFile', ('"{0}"' -f $logPath)
)
Write-Host "Bundle: $bundlePath"
Write-Host "ResoniteLink: $Port / Model: $Name / Position: $Position"
Write-Host "Report: $reportPath"
Write-Host "Log: $logPath"
if (-not $PSCmdlet.ShouldProcess("ResoniteLink $Port", "Send $bundlePath")) { return }
New-Item -ItemType Directory -Force -Path (Split-Path $reportPath), (Split-Path $logPath) | Out-Null
$sendProcess = Start-Process -FilePath $Unity -ArgumentList $sendArgs -WindowStyle Hidden -Wait -PassThru
if (-not (Test-Path -LiteralPath $reportPath)) {
    throw "Unityがレポートを作成せず終了しました (exit $($sendProcess.ExitCode))。ログ: $logPath"
}
$result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($sendProcess.ExitCode -ne 0 -or -not $result.completed) {
    throw "送信失敗: $($result.error)`nReport: $reportPath`nLog: $logPath"
}
$result | Select-Object name,completed,mode,rootId,playbackId,duration,effectLoopSeconds,growthPartCount,boundsCenter,boundsSize,bakeAdjustments
Write-Host '送信完了 (completed: True)'
