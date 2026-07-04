# CyberEye dev loop - VERIFY stage. Captures logs from the CURRENTLY-RUNNING app (launched via nebulaOS in
# MR mode) using device-side grep. Does NOT relaunch/force-stop (that would kill the MR instance + open a 2D
# one on the Beam Pro screen). Run this AFTER launching CyberEye from nebulaOS on the glasses.
# Usage: pwsh scripts/verify.ps1 [-Serial ...] [-Pkg com.jslade.cybereye]
# Exit: 0 PASS (CYBEREYE logs seen, no crash), 1 crash/ANR, 2 WARN (app not running / no logs).
# ASCII only: Windows PowerShell 5.1 mis-decodes non-ASCII in .ps1 files (no BOM).
[CmdletBinding()]
param(
  [string]$Serial = "",
  [string]$Pkg = "com.jslade.cybereye"
)
$dev = @(); if ($Serial) { $dev = @("-s", $Serial) }

$alive = (adb @dev shell pidof $Pkg 2>$null)
$cy = @(adb @dev shell "logcat -d | grep -F '[CYBEREYE]' | grep -vE 'CyberLog:|Controller:|Logger:|EyeCameraFeed:' | tail -n 60" 2>$null | Where-Object { $_ -match '\S' })
$cr = @(adb @dev shell "logcat -b crash -d | grep -F '$Pkg' | tail -n 20" 2>$null | Where-Object { $_ -match '\S' })

Write-Output "[verify] pid=$alive"
Write-Output "===== CYBEREYE ($($cy.Count)) ====="
$cy | ForEach-Object { $_ }
Write-Output "===== crash buffer for $Pkg ($($cr.Count)) ====="
$cr | ForEach-Object { $_ }

if ($cr.Count -gt 0) { Write-Output "[verify] RESULT=FAIL (crash/ANR)"; exit 1 }
if ($cy.Count -eq 0) { Write-Output "[verify] RESULT=WARN (no CYBEREYE logs; is CyberEye running via nebulaOS?)"; exit 2 }
Write-Output "[verify] RESULT=PASS"; exit 0
