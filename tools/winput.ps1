<#
.SYNOPSIS
  Minimal Win32 input automation for the Meta XR Simulator window (click at window-relative pixels, keys, drags).
.EXAMPLE
  ./tools/winput.ps1 -Process MetaXRSimulator -Click 182,772
  ./tools/winput.ps1 -Process MetaXRSimulator -Keys "{TAB}"
  ./tools/winput.ps1 -Process MetaXRSimulator -Drag 400,300,600,450 -Hold 400
#>
param(
  [string]$Process = "MetaXRSimulator",
  [string]$Click = "",
  [string]$RightClick = "",
  [string]$Drag = "",
  [int]$Hold = 120,
  [string]$Keys = "",
  [string]$Down = "",
  [string]$Up = "",
  [switch]$Info,
  [switch]$Maximize,
  [switch]$TopMost,
  [string]$ScreenClick = ""
)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class CursivisInput {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref System.Drawing.Point p);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@ -ReferencedAssemblies System.Drawing
[CursivisInput]::SetProcessDPIAware() | Out-Null
$ScreenPts = @($ScreenClick -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ })
if ($ScreenPts.Count -ge 2) { [CursivisInput]::SetProcessDPIAware() | Out-Null; [CursivisInput]::SetCursorPos($ScreenPts[0], $ScreenPts[1]) | Out-Null; Start-Sleep -Milliseconds 80; [CursivisInput]::mouse_event(0x02, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100; [CursivisInput]::mouse_event(0x04, 0, 0, 0, [UIntPtr]::Zero); Write-Output "screen click $($ScreenPts -join ',')"; exit 0 }
$p = Get-Process -Name $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { throw "No window for $Process" }
$h = $p.MainWindowHandle
[CursivisInput]::ShowWindow($h, $(if ([CursivisInput]::IsIconic($h)) { 9 } else { 5 })) | Out-Null
# Windows only lets the foreground process change focus; a synthetic Alt tap lifts that lock.
if ([CursivisInput]::GetForegroundWindow() -ne $h) { [CursivisInput]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [CursivisInput]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero); [CursivisInput]::SetForegroundWindow($h) | Out-Null }
if ($Info) { Write-Output ("foreground={0}" -f ([CursivisInput]::GetForegroundWindow() -eq $h)) }
if ($TopMost) { [CursivisInput]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x3) | Out-Null; Write-Output "topmost" }
if ($Maximize) { [CursivisInput]::ShowWindow($h, 3) | Out-Null; Start-Sleep -Milliseconds 400 }
Start-Sleep -Milliseconds 150
$r = New-Object CursivisInput+RECT
[CursivisInput]::GetWindowRect($h, [ref]$r) | Out-Null
if ($Info) { Write-Output ("window {0},{1} {2}x{3} title='{4}'" -f $r.Left, $r.Top, ($r.Right - $r.Left), ($r.Bottom - $r.Top), $p.MainWindowTitle) }
function To-Screen($x, $y) { return @(($r.Left + $x), ($r.Top + $y)) }
$ClickPts = @($Click -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ }); $RightPts = @($RightClick -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ }); $DragPts = @($Drag -split "[,; ]+" | Where-Object { $_ } | ForEach-Object { [int]$_ })
$LDOWN = 0x02; $LUP = 0x04; $RDOWN = 0x08; $RUP = 0x10
if ($ClickPts.Count -ge 2) {
  $s = To-Screen $ClickPts[0] $ClickPts[1]
  [CursivisInput]::SetCursorPos($s[0], $s[1]) | Out-Null; Start-Sleep -Milliseconds 60
  [CursivisInput]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds $Hold
  [CursivisInput]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
  Write-Output "clicked $($ClickPts -join ',') -> screen $($s -join ',')"
}
if ($RightPts.Count -ge 2) {
  $s = To-Screen $RightPts[0] $RightPts[1]
  [CursivisInput]::SetCursorPos($s[0], $s[1]) | Out-Null; Start-Sleep -Milliseconds 60
  [CursivisInput]::mouse_event($RDOWN, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds $Hold
  [CursivisInput]::mouse_event($RUP, 0, 0, 0, [UIntPtr]::Zero)
}
if ($DragPts.Count -ge 4) {
  $a = To-Screen $DragPts[0] $DragPts[1]; $b = To-Screen $DragPts[2] $DragPts[3]
  [CursivisInput]::SetCursorPos($a[0], $a[1]) | Out-Null; Start-Sleep -Milliseconds 80
  [CursivisInput]::mouse_event($LDOWN, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120
  $steps = 24
  for ($i = 1; $i -le $steps; $i++) {
    $x = [int]($a[0] + ($b[0] - $a[0]) * $i / $steps); $y = [int]($a[1] + ($b[1] - $a[1]) * $i / $steps)
    [CursivisInput]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds ([int]($Hold / $steps) + 8)
  }
  [CursivisInput]::mouse_event($LUP, 0, 0, 0, [UIntPtr]::Zero)
  Write-Output "dragged $($DragPts -join ',')"
}
if ($Keys) { [System.Windows.Forms.SendKeys]::SendWait($Keys); Write-Output "sent keys $Keys" }
$VK = @{ "SPACE"=0x20; "SHIFT"=0x10; "CTRL"=0x11; "ALT"=0x12; "W"=0x57; "A"=0x41; "S"=0x53; "D"=0x44; "Q"=0x51; "E"=0x45; "F"=0x46; "R"=0x52; "T"=0x54; "G"=0x47; "H"=0x48; "UP"=0x26; "DOWN"=0x28; "LEFT"=0x25; "RIGHT"=0x27; "1"=0x31; "2"=0x32; "3"=0x33; "4"=0x34; "5"=0x35; "KP1"=0x61; "KP2"=0x62; "KP3"=0x63; "KP4"=0x64; "B"=0x42; "N"=0x4E; "M"=0x4D; "I"=0x49; "U"=0x55; "Y"=0x59; "J"=0x4A; "LBRACKET"=0xDB; "RBRACKET"=0xDD }
if ($Down) { foreach ($k in $Down.Split(",")) { [CursivisInput]::keybd_event([byte]$VK[$k], 0, 0, [UIntPtr]::Zero) }; Write-Output "down $Down" }
if ($Up) { foreach ($k in $Up.Split(",")) { [CursivisInput]::keybd_event([byte]$VK[$k], 0, 2, [UIntPtr]::Zero) }; Write-Output "up $Up" }
