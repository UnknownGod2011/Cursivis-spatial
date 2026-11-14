<#
.SYNOPSIS
  Captures a series of simulator viewport screenshots while an autopilot run is in progress, tagging each with the
  latest [CursivisAuto] / [CursivisFlow] line so frames can be matched to app states.
#>
param([string]$Log = "test-results/local/sim/auto3.log", [string]$Prefix = "s", [int]$Count = 8, [int]$IntervalSec = 6, [string]$Crop = "826,144,736,767")
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
for ($i = 0; $i -lt $Count; $i++) {
  powershell -ExecutionPolicy Bypass -File tools/screenshot.ps1 -Out "test-results/local/shots/$Prefix$i.png" -Crop $Crop -MaxWidth 1600 | Out-Null
  $last = ""
  if (Test-Path $Log) { $last = (Select-String -Path $Log -Pattern "\[CursivisAuto\]|\[CursivisFlow\]" | Select-Object -Last 1).Line }
  if ($last.Length -gt 150) { $last = $last.Substring(0, 150) }
  Write-Output ("{0}{1} {2:HH:mm:ss} {3}" -f $Prefix, $i, (Get-Date), $last)
  Start-Sleep -Seconds $IntervalSec
}
