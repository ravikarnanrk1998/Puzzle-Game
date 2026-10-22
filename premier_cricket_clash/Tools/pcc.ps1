<#
.SYNOPSIS
  Premier Cricket Clash 2027 - Unity project helper (Windows PowerShell 5.1+).

.DESCRIPTION
  init   First open: creates ProjectSettings/Packages, adds URP, runs PCC setup.
  setup  Re-runs PCC > Setup Project (materials, boot scene, icon, player settings).
  apk    Builds Builds\PremierCricketClash2027.apk (test build, ARM64).
  aab    Builds Builds\PremierCricketClash2027.aab for Google Play (needs signing env vars).
  run    Installs the APK on the connected phone / emulator with adb and launches it.
  all    init + apk + run.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\pcc.ps1 all
#>
param(
    [ValidateSet('init', 'setup', 'apk', 'aab', 'run', 'all')]
    [string]$Step = 'all',
    [string]$Unity = ''
)

$ErrorActionPreference = 'Stop'
$Project = Split-Path -Parent $PSScriptRoot
$Logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Force $Logs | Out-Null

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

function Invoke-Unity([string[]]$UnityArgs, [string]$Name, [switch]$AllowFail) {
    $log = Join-Path $Logs "$Name.log"
    Write-Host "==> Unity $Name (log: $log)" -ForegroundColor Cyan
    $all = @('-batchmode', '-projectPath', "`"$Project`"", '-logFile', "`"$log`"") + $UnityArgs
    $p = Start-Process -FilePath $script:UnityExe -ArgumentList $all -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0 -and -not $AllowFail) {
        Get-Content $log -Tail 60 | Write-Host
        throw "Unity step '$Name' failed with exit code $($p.ExitCode). See $log"
    }
    return $p.ExitCode
}

function Get-BuiltInVersion([string]$Package) {
    $data = Join-Path (Split-Path -Parent $script:UnityExe) 'Data\Resources\PackageManager'
    $pj = Join-Path $data "BuiltInPackages\$Package\package.json"
    if (Test-Path $pj) { return (Get-Content $pj -Raw | ConvertFrom-Json).version }
    # Older layouts: read the version from a URP project template's manifest.
    $templates = Join-Path $data 'ProjectTemplates'
    if (Test-Path $templates) {
        $tmp = Join-Path $env:TEMP ('pcc_tpl_' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory $tmp | Out-Null
        try {
            foreach ($t in Get-ChildItem $templates -Filter '*.tgz' | Where-Object { $_.Name -match 'universal|urp' }) {
                & tar -xzf $t.FullName -C $tmp 'package/ProjectData~/Packages/manifest.json' 2>$null
                $m = Join-Path $tmp 'package\ProjectData~\Packages\manifest.json'
                if (Test-Path $m) {
                    $deps = (Get-Content $m -Raw | ConvertFrom-Json).dependencies
                    $v = $deps.$Package
                    if ($v) { return $v }
                }
            }
        } finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
    }
    return $null
}

function Add-Packages {
    $manifest = Join-Path $Project 'Packages\manifest.json'
    if (-not (Test-Path $manifest)) { throw "Packages\manifest.json was not created by Unity. See $Logs\init-open.log" }
    $json = Get-Content $manifest -Raw | ConvertFrom-Json
    $changed = $false
    foreach ($pkg in @('com.unity.render-pipelines.universal', 'com.unity.ugui')) {
        if ($json.dependencies.PSObject.Properties.Name -contains $pkg) { continue }
        $v = Get-BuiltInVersion $pkg
        if (-not $v) {
            # Core packages follow the editor: Unity 6000.N ships URP 17.N and uGUI 2.0.
            $minor = ((Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $script:UnityExe))) -split '\.')[1]
            $v = if ($pkg -eq 'com.unity.ugui') { '2.0.0' } else { "17.$minor.0" }
            Write-Host "    (guessing $pkg $v - if Unity reports it missing, add URP in Window > Package Manager and run: Tools\pcc.ps1 setup)" -ForegroundColor Yellow
        }
        $json.dependencies | Add-Member -NotePropertyName $pkg -NotePropertyValue $v
        Write-Host "    + $pkg $v"
        $changed = $true
    }
    if ($changed) {
        $text = $json | ConvertTo-Json -Depth 20
        [System.IO.File]::WriteAllText($manifest, $text, (New-Object System.Text.UTF8Encoding $false))
    }
}

function Step-Init {
    if (-not (Test-Path (Join-Path $Project 'Packages\manifest.json'))) {
        # First open creates ProjectSettings and the default manifest. Game scripts
        # cannot compile yet (no URP), so a non-zero exit here is expected.
        Invoke-Unity @('-quit') 'init-open' -AllowFail | Out-Null
    }
    Add-Packages
    Step-Setup
}

function Step-Setup {
    Invoke-Unity @('-buildTarget', 'Android', '-executeMethod', 'PCC.EditorTools.BuildTools.Setup', '-quit') 'setup' | Out-Null
}

function Step-Build([bool]$Aab) {
    $android = Join-Path (Split-Path -Parent $script:UnityExe) 'Data\PlaybackEngines\AndroidPlayer'
    if (-not (Test-Path $android)) { throw "Android Build Support is not installed for this editor. Add it in Unity Hub > Installs > (gear) > Add modules." }
    $method = if ($Aab) { 'PCC.EditorTools.BuildTools.BuildAab' } else { 'PCC.EditorTools.BuildTools.BuildApk' }
    Invoke-Unity @('-buildTarget', 'Android', '-executeMethod', $method) ($(if ($Aab) { 'build-aab' } else { 'build-apk' })) | Out-Null
    Get-ChildItem (Join-Path $Project 'Builds') | Format-Table Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
}

function Step-Run {
    $adb = Get-Command adb -ErrorAction SilentlyContinue
    $adbPath = if ($adb) { $adb.Source } else { Join-Path (Split-Path -Parent $script:UnityExe) 'Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' }
    if (-not (Test-Path $adbPath)) { throw 'adb not found.' }
    $apk = Join-Path $Project 'Builds\PremierCricketClash2027.apk'
    & $adbPath install -r $apk
    & $adbPath shell monkey -p com.rkgame.premiercricketclase -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Host 'Launched on device.' -ForegroundColor Green
}

$script:UnityExe = Find-Unity
Write-Host "Unity: $script:UnityExe"
switch ($Step) {
    'init' { Step-Init }
    'setup' { Step-Setup }
    'apk' { Step-Build $false }
    'aab' { Step-Build $true }
    'run' { Step-Run }
    'all' { Step-Init; Step-Build $false; Step-Run }
}
