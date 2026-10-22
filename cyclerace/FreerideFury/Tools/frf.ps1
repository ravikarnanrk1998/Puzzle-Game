<#
.SYNOPSIS
  Freeride Fury - Unity project helper (Windows PowerShell 5.1+).

.DESCRIPTION
  setup  Runs FRF > Setup Project (materials, boot scene, icon, player settings) and compiles.
  apk    Builds Builds\FreerideFury.apk (test build, ARM64).
  aab    Builds Builds\FreerideFury.aab for Google Play (needs signing, see README).
  win    Builds Builds\Windows\FreerideFury.exe (desktop test player).
  run    Installs the APK on the connected phone / emulator with adb and launches it.
  all    setup + apk + run.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\frf.ps1 all
#>
param(
    [ValidateSet('setup', 'apk', 'aab', 'win', 'run', 'all')]
    [string]$Step = 'all',
    [string]$Unity = ''
)

$ErrorActionPreference = 'Stop'
$Project = Split-Path -Parent $PSScriptRoot
$Logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Force $Logs | Out-Null
$Package = 'com.rkgame.freeridefury'

function Find-Unity {
    if ($Unity -and (Test-Path $Unity)) { return $Unity }
    $roots = @("$env:ProgramFiles\Unity\Hub\Editor", "${env:ProgramFiles(x86)}\Unity\Hub\Editor", "$env:LOCALAPPDATA\Unity\Hub\Editor")
    $found = foreach ($r in $roots) {
        if (Test-Path $r) { Get-ChildItem $r -Directory | Where-Object { $_.Name -like '6000.*' -and (Test-Path (Join-Path $_.FullName 'Editor\Unity.exe')) } }
    }
    $best = $found | Sort-Object { [version](($_.Name -replace '[a-z].*$', '')) } -Descending | Select-Object -First 1
    if (-not $best) {
        throw "Unity 6 (6000.x) not found. Install it from Unity Hub with 'Android Build Support' (+ OpenJDK, SDK & NDK), or pass -Unity <path to Unity.exe>."
    }
    return (Join-Path $best.FullName 'Editor\Unity.exe')
}

function Invoke-Unity([string[]]$UnityArgs, [string]$Name) {
    $log = Join-Path $Logs "$Name.log"
    Write-Host "==> Unity $Name (log: $log)" -ForegroundColor Cyan
    $all = @('-batchmode', '-projectPath', "`"$Project`"", '-logFile', "`"$log`"") + $UnityArgs
    $p = Start-Process -FilePath $script:UnityExe -ArgumentList $all -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) {
        Select-String -Path $log -Pattern 'error CS|Exception|\[FRF\]' | Select-Object -Last 40 | ForEach-Object { Write-Host $_.Line }
        throw "Unity step '$Name' failed with exit code $($p.ExitCode). See $log"
    }
}

function Step-Setup {
    Invoke-Unity @('-buildTarget', 'Android', '-executeMethod', 'FRF.EditorTools.BuildTools.SetupAndQuit') 'setup'
}

function Step-Build([string]$Kind) {
    if ($Kind -ne 'win') {
        $android = Join-Path (Split-Path -Parent $script:UnityExe) 'Data\PlaybackEngines\AndroidPlayer'
        if (-not (Test-Path $android)) { throw "Android Build Support is not installed for this editor. Add it in Unity Hub > Installs > (gear) > Add modules." }
    }
    switch ($Kind) {
        'apk' { Invoke-Unity @('-buildTarget', 'Android', '-executeMethod', 'FRF.EditorTools.BuildTools.BuildApk') 'build-apk' }
        'aab' { Invoke-Unity @('-buildTarget', 'Android', '-executeMethod', 'FRF.EditorTools.BuildTools.BuildAab') 'build-aab' }
        'win' { Invoke-Unity @('-buildTarget', 'Win64', '-executeMethod', 'FRF.EditorTools.BuildTools.BuildWindows') 'build-win' }
    }
    Get-ChildItem (Join-Path $Project 'Builds') -File -ErrorAction SilentlyContinue | Format-Table Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
}

function Step-Run {
    $sdkAdb = Join-Path $env:LOCALAPPDATA 'Android\sdk\platform-tools\adb.exe'
    $adb = Get-Command adb -ErrorAction SilentlyContinue
    $adbPath = if ($adb) { $adb.Source } elseif (Test-Path $sdkAdb) { $sdkAdb } else { Join-Path (Split-Path -Parent $script:UnityExe) 'Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' }
    if (-not (Test-Path $adbPath)) { throw 'adb not found.' }
    & $adbPath wait-for-device
    & $adbPath install -r (Join-Path $Project 'Builds\FreerideFury.apk')
    & $adbPath shell monkey -p $Package -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Host 'Launched on device.' -ForegroundColor Green
}

$script:UnityExe = Find-Unity
Write-Host "Unity: $script:UnityExe"
switch ($Step) {
    'setup' { Step-Setup }
    'apk' { Step-Build 'apk' }
    'aab' { Step-Build 'aab' }
    'win' { Step-Build 'win' }
    'run' { Step-Run }
    'all' { Step-Setup; Step-Build 'apk'; Step-Run }
}
