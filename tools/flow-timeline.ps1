<#
.SYNOPSIS
  Tails a player log while a recording runs and writes "elapsed_ms<TAB>line" for every flow / autopilot / UI event,
  so captions can be timed to what actually happened on screen (tools/label-video.ps1 reads the TSV).
.EXAMPLE
  ./tools/flow-timeline.ps1 -Log test-results/local/sim/demo.log -Out test-results/local/video/demo-timeline.tsv -Seconds 200
#>
param(
  [string]$Log = "test-results/local/sim/demo.log",
  [string]$Out = "test-results/local/video/timeline.tsv",
  [int]$Seconds = 200,
  [string]$Pattern = "\[CursivisFlow\]|\[CursivisAuto\]|\[CursivisUI\]|\[CursivisDiag\] theme|\[CursivisToast\]|\[CursivisSelect\]"
)
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$dir = Split-Path -Parent $Out
if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$sw = [Diagnostics.Stopwatch]::StartNew()
$offset = 0L
if (Test-Path $Log) { $offset = (Get-Item $Log).Length } # only events after the recording started
$writer = New-Object IO.StreamWriter((Join-Path (Get-Location) $Out), $false, (New-Object Text.UTF8Encoding($false)))
try {
  while ($sw.Elapsed.TotalSeconds -lt $Seconds) {
    if (Test-Path $Log) {
      $fs = [IO.File]::Open((Resolve-Path $Log), 'Open', 'Read', 'ReadWrite')
      try {
        if ($fs.Length -gt $offset) {
          $fs.Seek($offset, 'Begin') | Out-Null
          $reader = New-Object IO.StreamReader($fs)
          $chunk = $reader.ReadToEnd()
          $offset = $fs.Length
          foreach ($line in ($chunk -split "`r?`n")) {
            if ($line -match $Pattern) { $writer.WriteLine("{0}`t{1}" -f [int]$sw.Elapsed.TotalMilliseconds, $line.Trim()); $writer.Flush() }
          }
        }
      } finally { $fs.Dispose() }
    }
    Start-Sleep -Milliseconds 250
  }
} finally { $writer.Dispose() }
Write-Output "timeline -> $Out"
