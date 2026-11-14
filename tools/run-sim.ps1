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
  [string]$LogName = "player",
  [string]$Gfx = "-force-vulkan",
  # Overrides the backend baked into the build, e.g. http://127.0.0.1:8787 for a local backend with request logs.
  [string]$Backend = "",
  # Extra player arguments, e.g. "--cursivis-autopilot-delay=40,--cursivis-autopilot-pace=1.8" (comma separated).
  [string]$PlayerArgs = ""
)
$repo = Split-Path -Parent $PSScriptRoot
$runtimeJson = Join-Path $SimDir "meta_openxr_simulator.json"
if (-not (Test-Path $runtimeJson)) { throw "Simulator runtime not found: $runtimeJson" }
if (-not (Test-Path $Player)) { throw "Player not found: $Player (build it with CursivisBuild.BuildWindows)" }
$env:XR_RUNTIME_JSON = $runtimeJson
$env:XR_SELECTED_RUNTIME_JSON = $runtimeJson
$env:META_XRSIM_CONFIG_JSON = Join-Path $SimDir "config\sim_core_configuration.json"
# Meta XR Operator (Development builds ship it as an explicit OpenXR API layer): enable it for this process.
$layerDir = Join-Path (Split-Path -Parent $Player) "CursivisSpatial_Data\StreamingAssets\openxr\1\api_layers\explicit.d"
if (Test-Path (Join-Path $layerDir "XrApiLayer_METAX_operator.json")) {
  $env:XR_API_LAYER_PATH = $layerDir
  if ($env:CURSIVIS_FORCE_LAYER -eq "1") { $env:XR_ENABLE_API_LAYERS = "XR_APILAYER_METAX_operator" }
  Write-Output "Meta XR Operator layer enabled from $layerDir"
}
$logDir = Join-Path $repo "test-results/local/sim"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir "$LogName.log"
$args = @("-logFile", "`"$log`"", "-screen-width", "1280", "-screen-height", "720")
if ($Gfx) { $args += $Gfx }
if ($Autopilot) { $args += @("--cursivis-autopilot", "--cursivis-scenario=$Autopilot") }
if ($Backend) { $args += "--cursivis-backend=$Backend" }
if ($PlayerArgs) { $args += @($PlayerArgs -split "," | Where-Object { $_ }) }
$p = Start-Process -FilePath $Player -ArgumentList $args -PassThru
Write-Output "player pid=$($p.Id) log=$log"
if ($Seconds -gt 0) {
  if (-not $p.WaitForExit($Seconds * 1000)) { Write-Output "still running after $Seconds s" }
  else { Write-Output "player exited code=$($p.ExitCode)" }
}

