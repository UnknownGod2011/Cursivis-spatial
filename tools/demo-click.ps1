<#
.SYNOPSIS
  Drives the Meta XR Simulator with point-and-click (the simulated hand ray follows the mouse and a click is a pinch)
  and logs each action with its time since the recording started, for cutting and captioning the demo video.
.DESCRIPTION
  Coordinates are physical screen pixels. The recording start time is read from <video>.start.txt written by
  tools/record-sim.ps1. Entries go to test-results/local/video/clicks.tsv: "seconds<TAB>label".
.EXAMPLE
  ./tools/demo-click.ps1 -At "1227,330" -Label "pinch research abstract"
  ./tools/demo-click.ps1 -Drag "1000,300,1200,450" -Label "pinch-drag snip"
#>
param(
  [string]$At = "",
  [string]$Drag = "",
  [int]$HoldMs = 160,
  [int]$SettleMs = 1500,
  [int]$DragMs = 1600,
  [string]$Label = "",
  [string]$Start = "test-results/local/video/demo-raw.start.txt",
  [string]$Out = "test-results/local/video/clicks.tsv"
)
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class DemoInput {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[DemoInput]::SetProcessDPIAware() | Out-Null
$t0 = $null
if (Test-Path $Start) { $t0 = [datetime]::Parse((Get-Content $Start -Raw).Trim(), [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind) }
function Log($what) {
  $s = if ($t0) { ((Get-Date) - $t0).TotalSeconds } else { -1 }
  Add-Content -Path $Out -Value ("{0:0.00}`t{1}" -f $s, $what) -Encoding UTF8
  Write-Output ("{0:0.00}s {1}" -f $s, $what)
}
$p = @($At -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ })
if ($p.Count -ge 2) {
  [DemoInput]::SetCursorPos($p[0], $p[1]) | Out-Null
  Start-Sleep -Milliseconds $SettleMs    # the simulated hand turns toward the cursor over several frames at ~6 fps
  Log "click $($p[0]),$($p[1]) $Label"
  [DemoInput]::mouse_event(0x02, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds $HoldMs
  [DemoInput]::mouse_event(0x04, 0, 0, 0, [UIntPtr]::Zero)
}
$d = @($Drag -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ })
if ($d.Count -ge 4) {
  [DemoInput]::SetCursorPos($d[0], $d[1]) | Out-Null
  Start-Sleep -Milliseconds 400
  Log "drag $($d -join ',') $Label"
  [DemoInput]::mouse_event(0x02, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 300
  $steps = 30
  for ($i = 1; $i -le $steps; $i++) {
    $x = [int]($d[0] + ($d[2] - $d[0]) * $i / $steps); $y = [int]($d[1] + ($d[3] - $d[1]) * $i / $steps)
    [DemoInput]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds ([int]($DragMs / $steps))
  }
  Start-Sleep -Milliseconds 300
  [DemoInput]::mouse_event(0x04, 0, 0, 0, [UIntPtr]::Zero)
}
if ($p.Count -lt 2 -and $d.Count -lt 4 -and $Label) { Log $Label }
