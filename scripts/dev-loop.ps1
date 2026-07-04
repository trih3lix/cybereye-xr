# CyberEye dev loop - BUILD + INSTALL. Does NOT auto-launch or auto-verify: the real MR run must be started
# from nebulaOS on the Beam Pro (adb LAUNCHER opens a flat 2D window instead, where the Eye camera is inactive).
# After launching 'CyberEye' from nebulaOS in the glasses, run scripts/verify.ps1 to capture logs.
[CmdletBinding()]
param(
  [switch]$Release,
  [string]$Scene = "",
  [string]$Serial = "",
  [string]$WifiIp = "",
  [string]$Pkg = "com.jslade.cybereye"
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$apk  = if ($Release) { "C:\Users\jslade\CyberEyeXR\Builds\CyberEye-release.apk" } else { "C:\Users\jslade\CyberEyeXR\Builds\CyberEye.apk" }

Write-Output "################## BUILD ##################"
& "$here\build.ps1" -Release:$Release -Scene $Scene
if ($LASTEXITCODE -ne 0) { Write-Output "##### DEV-LOOP FAIL @ BUILD (code $LASTEXITCODE) #####"; exit 10 }

Write-Output "################## DEPLOY (install) #######"
& "$here\deploy.ps1" -Serial $Serial -WifiIp $WifiIp -Apk $apk -Pkg $Pkg
if ($LASTEXITCODE -ne 0) { Write-Output "##### DEV-LOOP FAIL @ DEPLOY (code $LASTEXITCODE) #####"; exit 11 }

Write-Output "################## DONE ###################"
Write-Output ">>> On the Beam Pro, launch 'CyberEye' from nebulaOS (runs it in the glasses / MR mode)."
Write-Output ">>> Then capture logs:  pwsh scripts/verify.ps1 -Serial `"$Serial`""
exit 0
