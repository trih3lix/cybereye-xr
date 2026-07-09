# CyberEye dev loop - DEPLOY stage. Installs the APK. Does NOT auto-launch by default:
# `adb monkey/am LAUNCHER` opens the app FLAT (2D) on the Beam Pro screen, where the XREAL MR session +
# Eye RGB camera are NOT fully active. The real run (glasses / MR / Eye) must be launched from nebulaOS's
# MR launcher on the Beam Pro. Pass -Launch2D only for a quick headless boot/crash sanity check.
# Use PowerShell for adb (git-bash mangles remote paths). Wireless is more stable than USB here.
[CmdletBinding()]
param(
  [string]$Serial = "",
  [string]$WifiIp = "",
  [string]$Apk = "",
  [string]$Pkg = "com.jslade.cybereye",
  [switch]$Launch2D
)
$ErrorActionPreference = "Stop"
$Project = Split-Path $PSScriptRoot -Parent   # repo root = scripts/..
if (-not $Apk) { $Apk = Join-Path $Project "Builds\CyberEye.apk" }
if ($WifiIp) { Write-Output "[deploy] adb connect ${WifiIp}:5555"; adb connect "${WifiIp}:5555" | Out-Null }
$dev = @(); if ($Serial) { $dev = @("-s", $Serial) }

Write-Output "[deploy] devices:"; adb devices -l
if (-not (Test-Path $Apk)) { Write-Output "[deploy] APK not found: $Apk"; exit 3 }

Write-Output "[deploy] install -r -g $Apk"
adb @dev install -r -g $Apk
if ($LASTEXITCODE -ne 0) { Write-Output "[deploy] INSTALL FAILED ($LASTEXITCODE). Try adb reconnect / check authorized."; exit 1 }

if ($Launch2D) {
  Write-Output "[deploy] Launch2D: launching FLAT on Beam Pro screen (sanity only, NOT MR/glasses)"
  adb @dev shell monkey -p $Pkg -c android.intent.category.LAUNCHER 1 | Out-Null
} else {
  Write-Output "[deploy] installed OK. >>> On the Beam Pro, launch 'CyberEye' from nebulaOS to run it in the glasses (MR mode)."
}
exit 0
