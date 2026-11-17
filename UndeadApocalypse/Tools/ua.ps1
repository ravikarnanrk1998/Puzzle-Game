# Undead Apocalypse: Infinite War - command-line setup, builds, tests and device runs.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\ua.ps1 <setup|compile|win|apk|aab|keystore|install|run|shots|logcat|test|all> [-plan all]
param([string]$cmd = "all", [string]$plan = "all")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$hub = "C:\Program Files\Unity\Hub\Editor"
$ver = (Get-ChildItem $hub | Where-Object { $_.Name -like "6000.*" } | Sort-Object Name -Descending | Select-Object -First 1).Name
$unity = Join-Path $hub "$ver\Editor\Unity.exe"
$android = Join-Path $hub "$ver\Editor\Data\PlaybackEngines\AndroidPlayer"
$adb = "$env:LOCALAPPDATA\Android\sdk\platform-tools\adb.exe"
if (-not (Test-Path $adb)) { $adb = Join-Path $android "SDK\platform-tools\adb.exe" }
$pkg = "com.rkgame.undeadapocalypse"

function Unity([string]$method, [string]$log, [string[]]$extra = @()) {
    $logPath = Join-Path $root "Logs\$log.log"
    New-Item -ItemType Directory -Force (Join-Path $root "Logs") | Out-Null
    Write-Host "Unity $ver -> $method (log: Logs\$log.log)"
    $p = Start-Process -FilePath $unity -ArgumentList (@("-batchmode", "-projectPath", "`"$root`"", "-executeMethod", $method, "-logFile", "`"$logPath`"") + $extra) -PassThru -Wait
    Select-String -Path $logPath -Pattern "\[UA\]|error CS|Scripts have compiler errors" | ForEach-Object { $_.Line.Trim() } | Select-Object -First 80
    if ($p.ExitCode -ne 0) { throw "$method failed (exit $($p.ExitCode))" }
}

function Keystore {
    $dir = Join-Path $root "Signing"
    $ks = Join-Path $dir "ua-upload.keystore"
    if (Test-Path $ks) { Write-Host "Keystore exists: $ks"; return }
    New-Item -ItemType Directory -Force $dir | Out-Null
    $bytes = New-Object byte[] 18
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $pass = [Convert]::ToBase64String($bytes).Replace("+", "x").Replace("/", "y").Replace("=", "")
    $keytool = Join-Path $android "OpenJDK\bin\keytool.exe"
    & $keytool -genkeypair -v -storetype PKCS12 -keystore $ks -alias upload -keyalg RSA -keysize 2048 -validity 10000 -storepass $pass -keypass $pass -dname "CN=RK Games, OU=Games, O=RK Games, L=Chennai, C=IN" | Out-Null
    "storeFile=ua-upload.keystore`nstorePassword=$pass`nkeyAlias=upload`nkeyPassword=$pass" | Out-File -Encoding ascii (Join-Path $dir "keystore.properties")
    "Undead Apocalypse upload key`nKeystore: ua-upload.keystore (PKCS12)`nAlias: upload`nPassword: $pass`n`nBACK THIS FOLDER UP. Every future Play Store update must be signed with this key." | Out-File -Encoding utf8 (Join-Path $dir "KEYSTORE-INFO.txt")
    Write-Host "Created upload keystore in Signing\ (see KEYSTORE-INFO.txt)"
}

function Install {
    & $adb wait-for-device
    & $adb install -r (Join-Path $root "Builds\UndeadApocalypse.apk")
}

function Run([string]$p) {
    & $adb shell am force-stop $pkg
    & $adb shell "am start -n $pkg/com.unity3d.player.UnityPlayerGameActivity -e unity '-uaAuto -uaShots shots -uaPlan $p'"
}

switch ($cmd) {
    "setup" { Unity "UA.EditorTools.BuildTools.CliSetup" "setup" }
    "compile" { Unity "UA.EditorTools.BuildTools.CliCompile" "compile" }
    "art" { Unity "UA.EditorTools.ArtPipeline.CliBuild" "art_build" }
    "artinspect" { Unity "UA.EditorTools.ArtPipeline.CliInspect" "art_inspect" }
    "artpreview" { Unity "UA.EditorTools.ArtPipeline.CliPreview" "art_preview" }
    "icon" { Unity "UA.EditorTools.BuildTools.CliIcon" "icon" }
    "thumbs" { Unity "UA.EditorTools.BuildTools.CliThumbs" "thumbs" }
    # -nographics: frames are decoded and encoded on the CPU (a GPU device in batch mode never frees its upload memory).
    "promovideo" { Unity "UA.EditorTools.BuildTools.CliPromoVideo" "promo_video" @("-nographics") }
    "win" { Unity "UA.EditorTools.BuildTools.CliWin" "build_win" }
    "apk" { Unity "UA.EditorTools.BuildTools.CliApk" "build_apk" }
    "keystore" { Keystore }
    "aab" { Keystore; Unity "UA.EditorTools.BuildTools.CliAab" "build_aab" }
    "install" { Install }
    "run" { Run $plan }
    "shots" {
        $out = Join-Path $root "Builds\shots_android"
        New-Item -ItemType Directory -Force $out | Out-Null
        & $adb pull "/storage/emulated/0/Android/data/$pkg/files/shots" $out
    }
    "logcat" { & $adb logcat -d -s Unity | Select-String "\[UA\]|Exception|Error" | Select-Object -Last 120 }
    "test" {
        Push-Location (Join-Path $root "Tools\CoreTests")
        dotnet run -c Release
        Pop-Location
    }
    "all" { Unity "UA.EditorTools.BuildTools.CliSetup" "setup"; Unity "UA.EditorTools.BuildTools.CliApk" "build_apk"; Install; Run $plan }
    default { Write-Host "Unknown command $cmd" }
}
