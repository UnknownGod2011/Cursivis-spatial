<#
.SYNOPSIS
  Records the simulator demo: starts the player with the paced test autopilot, pins the simulator window on top,
  starts recording the left-eye viewport with ffmpeg, performs a real simulated-hand pinch (point-and-click: the
  Interaction SDK hand ray follows the mouse, a click is a pinch) on the code document before the autopilot begins,
  and stops when the autopilot reports. Idle time at the start is trimmed when the video is cut.
.EXAMPLE
  ./tools/demo-run.ps1 -Scenario "code,notice,label,snip,research" -Delay 80 -Pace 1.5
#>
param(
  [string]$Scenario = "code,notice,label,snip,research",
  [int]$Delay = 80,
  [double]$Pace = 1.5,
  [string]$Name = "demo-final",
  [string]$PinchAt = "1430,614",
  [int]$MaxSeconds = 480
)
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$k = "HKCU:\Software\Cursivis\Cursivis Spatial"
if (Test-Path $k) {
  foreach ($pr in (Get-ItemProperty $k).PSObject.Properties) {
    if ($pr.Name -like "cursivis.onboarded*") { Set-ItemProperty -Path $k -Name $pr.Name -Value 1 -Type DWord }
    if ($pr.Name -like "cursivis.theme.dark*") { Set-ItemProperty -Path $k -Name $pr.Name -Value 1 -Type DWord }
  }
}
$log = "test-results/local/sim/$Name.log"
$video = "test-results/local/video/$Name.mkv"
$startFile = [IO.Path]::ChangeExtension($video, ".start.txt")
Remove-Item $log, $video, $startFile, "test-results/local/video/clicks.tsv" -ErrorAction SilentlyContinue
$pace = $Pace.ToString([Globalization.CultureInfo]::InvariantCulture)
Start-Process powershell -WindowStyle Hidden -ArgumentList "-ExecutionPolicy Bypass -File tools/run-sim.ps1 -LogName $Name -Autopilot $Scenario -PlayerArgs --cursivis-autopilot-delay=$Delay,--cursivis-autopilot-pace=$pace"
for ($i = 0; $i -lt 90; $i++) { Start-Sleep 2; if ((Test-Path $log) -and (Select-String -Path $log -Pattern "autopilot starting" -Quiet)) { break } }
Write-Output "app started $(Get-Date -Format HH:mm:ss.fff)"
metavr xrsim input point-and-click --active true 2>&1 | Out-Null
powershell -ExecutionPolicy Bypass -File tools/winput.ps1 -Process MetaXRSimulator -TopMost -Maximize | Out-Null
Start-Process powershell -WindowStyle Hidden -ArgumentList "-ExecutionPolicy Bypass -File tools/record-sim.ps1 -Out $video -Seconds $MaxSeconds -Fps 15"
for ($i = 0; $i -lt 30 -and -not (Test-Path $startFile); $i++) { Start-Sleep 1 }
Start-Sleep 4
Write-Output "recording $(Get-Date -Format HH:mm:ss.fff)"
# genuine simulated-hand pinch: the right hand's ray (Interaction SDK) selects the code document
powershell -ExecutionPolicy Bypass -File tools/demo-click.ps1 -At $PinchAt -Label "hand pinch on reviews.js" -Start $startFile
Write-Output "pinched $(Get-Date -Format HH:mm:ss.fff)"
$t0 = Get-Date
while (((Get-Date) - $t0).TotalSeconds -lt $MaxSeconds) {
  Start-Sleep 3
  if (Select-String -Path $log -Pattern "\[CursivisAuto\] report" -Quiet) { break }
}
Start-Sleep 4
Get-Process ffmpeg -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
Select-String -Path $log -Pattern "\[CursivisAuto\] (report|begin|\S+ FAIL)|\[CursivisSelect\]" | ForEach-Object { $_.Line.Substring(0, [Math]::Min(140, $_.Line.Length)) }
Write-Output "done $(Get-Date -Format HH:mm:ss)"
