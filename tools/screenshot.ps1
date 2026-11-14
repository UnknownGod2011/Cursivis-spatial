<#
.SYNOPSIS
  Captures the desktop or one window (by process name) to a PNG for automated visual checks.
.EXAMPLE
  ./tools/screenshot.ps1 -Out test-results/local/shots/desk.png
  ./tools/screenshot.ps1 -Process MetaXRSimulator -Out test-results/local/shots/sim.png
#>
param([string]$Out = "test-results/local/shots/screen.png", [string]$Process = "", [int]$MaxWidth = 1600, [string]$Crop = "")
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class CursivisWin {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[CursivisWin]::SetProcessDPIAware() | Out-Null
$dir = Split-Path -Parent $Out
if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
if ($Process) {
  $p = Get-Process -Name $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $p) { throw "No window for process $Process" }
  $h = $p.MainWindowHandle
  [CursivisWin]::ShowWindow($h, $(if ([CursivisWin]::IsIconic($h)) { 9 } else { 5 })) | Out-Null
  $r = New-Object CursivisWin+RECT
  [CursivisWin]::GetWindowRect($h, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $hgt = $r.Bottom - $r.Top
  $bmp = New-Object System.Drawing.Bitmap $w, $hgt
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  $ok = [CursivisWin]::PrintWindow($h, $hdc, 2)
  $g.ReleaseHdc($hdc)
  if (-not $ok) { $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size) }
} else {
  $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
}
$g.Dispose()
$cp = @($Crop -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ })
if ($cp.Count -eq 4) { $c = $bmp.Clone((New-Object System.Drawing.Rectangle $cp[0], $cp[1], $cp[2], $cp[3]), $bmp.PixelFormat); $bmp.Dispose(); $bmp = $c }
if ($bmp.Width -gt $MaxWidth) {
  $scale = $MaxWidth / $bmp.Width
  $small = New-Object System.Drawing.Bitmap ([int]($bmp.Width * $scale)), ([int]($bmp.Height * $scale))
  $g2 = [System.Drawing.Graphics]::FromImage($small)
  $g2.InterpolationMode = "HighQualityBicubic"
  $g2.DrawImage($bmp, 0, 0, $small.Width, $small.Height)
  $g2.Dispose(); $bmp.Dispose(); $bmp = $small
}
$bmp.Save((Resolve-Path -LiteralPath (Split-Path -Parent (Join-Path (Get-Location) $Out))).Path + "\" + (Split-Path -Leaf $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved $Out"
