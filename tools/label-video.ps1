<#
.SYNOPSIS
  Turns a raw simulator recording into the labelled 1920x1080 demo video: title card, kept segments with a caption
  panel and a permanent "Meta XR Simulator footage" label, end card.
.DESCRIPTION
  -Segments is a TSV with one row per kept segment of the raw video:
      raw_start_s <TAB> raw_end_s <TAB> title <TAB> subtitle
  Use "|" inside title/subtitle for a line break. Only idle time (between a finished result and the next action)
  should be cut; never cut the wait between an action and its AI result, so latency is shown honestly.
.EXAMPLE
  ./tools/label-video.ps1 -Raw test-results/local/video/demo-raw.mp4 -Segments submission/media/demo-segments.tsv -Out submission/media/cursivis-spatial-demo.mp4
#>
param(
  [string]$Raw = "test-results/local/video/demo-raw.mp4",
  [string]$Segments = "submission/media/demo-segments.tsv",
  [string]$Out = "submission/media/cursivis-spatial-demo.mp4",
  [int]$Fps = 15
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$work = "test-results/local/video/build"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work | Out-Null
$fontB = "unity/CursivisSpatial/Assets/Cursivis/Fonts/Inter-Bold.ttf"
$fontS = "unity/CursivisSpatial/Assets/Cursivis/Fonts/Inter-SemiBold.ttf"
$fontR = "unity/CursivisSpatial/Assets/Cursivis/Fonts/Inter-Regular.ttf"
$enc = New-Object System.Text.UTF8Encoding($false)
function TextFile($name, $text) {
  $path = "$work/$name.txt"
  [IO.File]::WriteAllText((Join-Path $repo $path), ($text -replace '\|', "`n"), $enc)
  return $path
}
$bg = "0x0F1317"
$codec = @("-c:v", "libx264", "-preset", "veryfast", "-crf", "19", "-pix_fmt", "yuv420p", "-r", "$Fps", "-an")
$label = TextFile "label" "Meta XR Simulator footage (Quest 3 profile)|Live AI responses, real app build. No headset was used."
$parts = New-Object System.Collections.Generic.List[string]

# Title card (3 s)
$tt = TextFile "title_name" "Cursivis Spatial"
$tg = TextFile "title_tag" "Selection is Context. Gesture is Intent. Cursivis is Action."
$tl = TextFile "title_label" "Meta XR Simulator footage with live AI responses"
$vf = "drawtext=fontfile=${fontB}:textfile=${tt}:expansion=none:fontsize=104:fontcolor=white:x=(w-text_w)/2:y=390," +
      "drawtext=fontfile=${fontS}:textfile=${tg}:expansion=none:fontsize=40:fontcolor=0xC9D0D8:x=(w-text_w)/2:y=540," +
      "drawtext=fontfile=${fontR}:textfile=${tl}:expansion=none:fontsize=28:fontcolor=0x8A929B:x=(w-text_w)/2:y=640"
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=${bg}:s=1920x1080:r=${Fps}:d=3.5" -vf $vf @codec "$work/p00.mp4"
$parts.Add("p00.mp4")

$rows = Get-Content $Segments -Encoding UTF8 | Where-Object { $_ -and -not $_.StartsWith("#") }
$i = 0; $total = 3.5
foreach ($row in $rows) {
  $i++
  $c = $row -split "`t"
  $s = [double]::Parse($c[0], [Globalization.CultureInfo]::InvariantCulture)
  $e = [double]::Parse($c[1], [Globalization.CultureInfo]::InvariantCulture)
  $dur = [Math]::Round($e - $s, 2)
  $total += $dur
  $title = TextFile ("t{0:00}" -f $i) $c[2]
  $sub = TextFile ("s{0:00}" -f $i) ($(if ($c.Count -gt 3) { $c[3] } else { "" }))
  $step = TextFile ("n{0:00}" -f $i) ("{0} / {1}" -f $i, $rows.Count)
  $brand = TextFile "brand" "CURSIVIS SPATIAL"
  $ss = $s.ToString([Globalization.CultureInfo]::InvariantCulture); $dd = $dur.ToString([Globalization.CultureInfo]::InvariantCulture)
  $fc = "[0:v]scale=-2:1040,setsar=1[v];[1:v][v]overlay=40:20:shortest=1," +
        "drawtext=fontfile=${fontR}:textfile=${label}:expansion=none:fontsize=24:fontcolor=0x8A929B:x=1100:y=56:line_spacing=10," +
        "drawtext=fontfile=${fontS}:textfile=${step}:expansion=none:fontsize=26:fontcolor=0x5FD3F3:x=1100:y=330," +
        "drawtext=fontfile=${fontS}:textfile=${title}:expansion=none:fontsize=50:fontcolor=white:x=1100:y=380:line_spacing=14," +
        "drawtext=fontfile=${fontR}:textfile=${sub}:expansion=none:fontsize=30:fontcolor=0xB9C0C8:x=1100:y=560:line_spacing=12," +
        "drawtext=fontfile=${fontB}:textfile=${brand}:expansion=none:fontsize=28:fontcolor=0xD5DAE0:x=1100:y=990[out]"
  & ffmpeg -hide_banner -loglevel error -y -ss $ss -t $dd -i $Raw -f lavfi -i "color=c=${bg}:s=1920x1080:r=${Fps}" -filter_complex $fc -map "[out]" @codec ("$work/p{0:00}.mp4" -f $i)
  $parts.Add(("p{0:00}.mp4" -f $i))
}

# End card (6 s)
$e1 = TextFile "end_name" "Cursivis Spatial"
$e2 = TextFile "end_line" "Select anything. Cursivis understands what you might want to do next."
$e3 = TextFile "end_tech" "Meta Quest 3 / 3S  ·  Unity 6  ·  Meta XR Core, Interaction SDK, MR Utility Kit 209|Passthrough Camera API  ·  Gemini through a keyless-client backend"
$e4 = TextFile "end_repo" "github.com/UnknownGod2011/Cursivis-spatial"
$e5 = TextFile "end_note" "Recorded in Meta XR Simulator. On-device behaviour has not been verified yet."
$vf = "drawtext=fontfile=${fontB}:textfile=${e1}:expansion=none:fontsize=88:fontcolor=white:x=(w-text_w)/2:y=300," +
      "drawtext=fontfile=${fontS}:textfile=${e2}:expansion=none:fontsize=38:fontcolor=0xC9D0D8:x=(w-text_w)/2:y=440," +
      "drawtext=fontfile=${fontR}:textfile=${e3}:expansion=none:fontsize=28:fontcolor=0x9AA2AB:x=(w-text_w)/2:y=560:line_spacing=14," +
      "drawtext=fontfile=${fontS}:textfile=${e4}:expansion=none:fontsize=32:fontcolor=0x5FD3F3:x=(w-text_w)/2:y=690," +
      "drawtext=fontfile=${fontR}:textfile=${e5}:expansion=none:fontsize=26:fontcolor=0x8A929B:x=(w-text_w)/2:y=780"
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=${bg}:s=1920x1080:r=${Fps}:d=6" -vf $vf @codec "$work/p99.mp4"
$parts.Add("p99.mp4")
$total += 6

$list = ($parts | ForEach-Object { "file '$_'" }) -join "`n"
[IO.File]::WriteAllText((Join-Path $repo "$work/list.txt"), $list, $enc)
$outDir = Split-Path -Parent $Out
if ($outDir) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
& ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i "$work/list.txt" -c copy -movflags +faststart $Out
$d = & ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $Out
Write-Output ("{0}: {1:n1} s (planned {2:n1} s), {3:n1} MB" -f $Out, [double]$d, $total, ((Get-Item $Out).Length / 1MB))
if ([double]$d -ge 180) { Write-Warning "Video is 3:00 or longer; the competition requires under 3 minutes." }
