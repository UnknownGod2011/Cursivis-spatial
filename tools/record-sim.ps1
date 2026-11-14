<#
.SYNOPSIS
  Records the Meta XR Simulator viewport to MP4 with ffmpeg (the 207.1 simulator build has no native recorder:
  `metavr xrsim record start` reports "build doesn't include the recording service").
.DESCRIPTION
  Captures a screen region with gdigrab (cursor hidden) and encodes H.264 with a light preset so the capture does not
  steal much CPU from the simulator. Default region = left-eye viewport of the maximized simulator window on a
  1920x1080 screen; measure yours with tools/screenshot.ps1 if the layout differs.
.EXAMPLE
  ./tools/record-sim.ps1 -Out test-results/local/video/demo-raw.mp4 -Seconds 200
#>
param(
  [string]$Out = "test-results/local/video/sim.mp4",
  [int]$Seconds = 60,
  [int]$Fps = 15,
  [string]$Region = "838,142,778,814",
  # Optional: player log to timestamp alongside the video (writes <Out>.timeline.tsv via tools/flow-timeline.ps1)
  [string]$Log = ""
)
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$r = @($Region -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ })
if ($r.Count -ne 4) { throw "Region must be x,y,w,h" }
$w = $r[2] - ($r[2] % 2); $h = $r[3] - ($r[3] % 2)
$dir = Split-Path -Parent $Out
if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$ff = (Get-Command ffmpeg -ErrorAction Stop).Source
$args = @("-hide_banner", "-loglevel", "warning", "-y",
  "-f", "gdigrab", "-framerate", "$Fps", "-draw_mouse", "0",
  "-offset_x", "$($r[0])", "-offset_y", "$($r[1])", "-video_size", "${w}x${h}", "-i", "desktop",
  "-t", "$Seconds", "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", $Out)
Write-Output "recording ${w}x${h}@$Fps for $Seconds s -> $Out"
if ($Log) {
  $tl = [IO.Path]::ChangeExtension($Out, ".timeline.tsv")
  Start-Process powershell -WindowStyle Hidden -ArgumentList "-ExecutionPolicy Bypass -File `"$(Join-Path $PSScriptRoot 'flow-timeline.ps1')`" -Log `"$Log`" -Out `"$tl`" -Seconds $($Seconds + 5)"
  Write-Output "timeline -> $tl"
}
# wall-clock start, used by tools/demo-click.ps1 to timestamp actions against the video
Set-Content -Path ([IO.Path]::ChangeExtension($Out, ".start.txt")) -Value (Get-Date).ToString("o") -Encoding ASCII
& $ff @args
if (Test-Path $Out) { Write-Output ("saved {0} ({1:n1} MB)" -f $Out, ((Get-Item $Out).Length / 1MB)) }
