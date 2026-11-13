<#
.SYNOPSIS
  Runs a headless Unity job for the Cursivis Spatial project and summarizes errors.
.EXAMPLE
  ./tools/unity-batch.ps1 -Method Cursivis.Spatial.EditorTools.ProjectSetup.RunBatch -Target StandaloneWindows64 -Name setup
  ./tools/unity-batch.ps1 -Method Cursivis.Spatial.EditorTools.BuildPipeline.BuildAndroid -Target Android -Name apk -Quit
  ./tools/unity-batch.ps1 -Tests EditMode -Name editmode
#>
param(
  [string]$Method = "",
  [string]$Target = "StandaloneWindows64",
  [string]$Name = "job",
  [switch]$Quit,
  [ValidateSet("", "EditMode", "PlayMode")][string]$Tests = "",
  [int]$TimeoutMinutes = 90,
  [string[]]$ExtraArgs = @()
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "unity/CursivisSpatial"
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe"
$logDir = Join-Path $repo "test-results/local/unity"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir "$Name.log"
if (Test-Path $log) { Remove-Item $log -Force }

$args = @("-batchmode", "-projectPath", "`"$project`"", "-buildTarget", $Target, "-logFile", "`"$log`"")
if ($Method) { $args += @("-executeMethod", $Method) }
if ($Quit) { $args += "-quit" }
if ($Tests) {
  $results = Join-Path $logDir "$Name-results.xml"
  $args += @("-runTests", "-testPlatform", $Tests, "-testResults", "`"$results`"")
}
$args += $ExtraArgs

$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $unity -ArgumentList $args -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
  Write-Output "TIMEOUT after $TimeoutMinutes min; killing Unity"
  Stop-Process -Id $p.Id -Force
}
$sw.Stop()
Write-Output ("exit={0} elapsed={1:n1}min log={2}" -f $p.ExitCode, $sw.Elapsed.TotalMinutes, $log)
if (Test-Path $log) {
  $errors = Select-String -Path $log -Pattern "error CS\d+|Exception:|\[CursivisSetup\]|\[CursivisBuild\]|\[CursivisScene\]|Build (succeeded|failed)|Compilation failed|Scripts have compiler errors" |
    Select-Object -First 80
  $errors | ForEach-Object { $l = $_.Line; if ($l.Length -gt 300) { $l = $l.Substring(0, 300) }; $l }
}
if ($Tests -and (Test-Path $results)) {
  [xml]$x = Get-Content $results
  $r = $x.'test-run'
  Write-Output ("tests total={0} passed={1} failed={2} skipped={3}" -f $r.total, $r.passed, $r.failed, $r.skipped)
  $x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { "FAILED: $($_.fullname) :: $($_.failure.message.'#cdata-section')" }
}
exit $p.ExitCode
