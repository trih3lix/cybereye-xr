# CyberEye dev loop — BUILD stage. Runs Unity in batchmode to produce the APK.
# First build on a fresh project imports all packages + IL2CPP + Gradle => can take 20-40 min.
# Usage: pwsh scripts/build.ps1 [-Release] [-Scene "Assets/.../HelloMR.unity"]
[CmdletBinding()]
param(
  [switch]$Release,
  [string]$Scene = "",
  [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.0.78f1\Editor\Unity.exe"
)
$ErrorActionPreference = "Stop"
$Project = Split-Path $PSScriptRoot -Parent   # repo root = scripts/..
if (-not (Test-Path $Unity)) { Write-Output "[build] Unity not found: $Unity"; exit 4 }
$log = Join-Path $Project "Builds\unity-build.log"
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
if (Test-Path $log) { Remove-Item $log -Force }

$uargs = @("-batchmode","-quit","-projectPath",$Project,"-buildTarget","Android",
           "-executeMethod","BuildScript.PerformAndroidBuild","-logFile",$log)
if ($Release) { $uargs += "-release" }
if ($Scene)   { $uargs += @("-scene",$Scene) }

# Windows PowerShell Start-Process does NOT quote array elements, and the sample scene
# path contains spaces ("XREAL XR Plugin"), so build one command line quoting spaced args.
$argline = ($uargs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
Write-Output "[build] $Unity"
Write-Output "[build] $argline"
$t0 = Get-Date
$p = Start-Process -FilePath $Unity -ArgumentList $argline -PassThru -NoNewWindow -Wait
$dt = [int]((Get-Date) - $t0).TotalSeconds
Write-Output "[build] Unity exited code=$($p.ExitCode) after ${dt}s"

if (Test-Path $log) {
  Write-Output "----- build log (filtered tail) -----"
  Select-String -Path $log -Pattern "CYBEREYE-BUILD|error CS\d|BuildFailedException|UnityException|Exception:|Gradle|FAILED|Aborting batchmode|error:" |
    Select-Object -Last 60 | ForEach-Object { $_.Line }
}
exit $p.ExitCode
