<#
.SYNOPSIS
  Runs the Cursivis Spatial Windows player against the Meta XR Simulator (OpenXR runtime override per process,
  same environment variables the Meta XR Core SDK sets when you click "Activate" in the editor).
.EXAMPLE
  ./tools/run-sim.ps1                      # interactive run
  ./tools/run-sim.ps1 -Autopilot all -Seconds 240
#>
param(
  [string]$SimDir = "C:\Users\Admin\MetaXRSimulator\PFiles\MetaXRSimulator\v207.1",
  [string]$Player = (Join-Path (Split-Path -Parent $PSScriptRoot) "builds/windows/CursivisSpatial.exe"),
  [string]$Autopilot = "",
  [int]$Seconds = 0,
  [string]$LogName = "player"
)
$repo = Split-Path -Parent $PSScriptRoot
$runtimeJson = Join-Path $SimDir "meta_openxr_simulator.json"
if (-not (Test-Path $runtimeJson)) { throw "Simulator runtime not found: $runtimeJson" }
if (-not (Test-Path $Player)) { throw "Player not found: $Player (build it with CursivisBuild.BuildWindows)" }
$env:XR_RUNTIME_JSON = $runtimeJson
$env:XR_SELECTED_RUNTIME_JSON = $runtimeJson
$env:META_XRSIM_CONFIG_JSON = Join-Path $SimDir "config\sim_core_configuration.json"
$logDir = Join-Path $repo "test-results/local/sim"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir "$LogName.log"
$args = @("-logFile", "`"$log`"", "-screen-width", "1280", "-screen-height", "720")
if ($Autopilot) { $args += @("--cursivis-autopilot", "--cursivis-scenario=$Autopilot") }
$p = Start-Process -FilePath $Player -ArgumentList $args -PassThru
Write-Output "player pid=$($p.Id) log=$log"
if ($Seconds -gt 0) {
  if (-not $p.WaitForExit($Seconds * 1000)) { Write-Output "still running after $Seconds s" }
  else { Write-Output "player exited code=$($p.ExitCode)" }
}
