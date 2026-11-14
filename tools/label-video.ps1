<#
.SYNOPSIS
  Turns a raw simulator recording into the labelled 1920x1080 demo video: title card, kept segments (cropped to the
  UI area and enlarged) with a caption panel and a permanent "Meta XR Simulator footage" label, end card.
.DESCRIPTION
  -Segments is a TSV, one row per segment:  start_s <TAB> end_s <TAB> title <TAB> subtitle <TAB> note
    - end_s "+N" holds the frame at start_s for N seconds (a freeze so chips can be read).
    - "|" inside title/subtitle/note is a line break.
  Only idle time should be cut (before the first action, between scenarios). Waits between an action and its AI
  result are kept, so latency is shown honestly.
.EXAMPLE
  ./tools/label-video.ps1 -Raw test-results/local/video/demo-final.mkv -Segments submission/media/demo-segments.tsv
#>
param(
  [string]$Raw = "test-results/local/video/demo-final.mkv",
  [string]$Segments = "submission/media/demo-segments.tsv",
  [string]$Out = "submission/media/cursivis-spatial-demo.mp4",
  [string]$Crop = "548:460:230:255",   # w:h:x:y of the raw left-eye capture that contains the UI
  [int]$VideoHeight = 1000,
  [int]$Fps = 15
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$inv = [Globalization.CultureInfo]::InvariantCulture
$work = "test-results/local/video/build"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work | Out-Null
$fontB = "unity/CursivisSpatial/Assets/Cursivis/Fonts/Inter-Bold.ttf"
$fontS = "unity/CursivisSpatial/Assets/Cursivis/Fonts/Inter-SemiBold.ttf"
$fontR = "unity/CursivisSpatial/Assets/Cursivis/Fonts/Inter-Regular.ttf"
$enc = New-Object System.Text.UTF8Encoding($false)
function TextFile($name, $text) {
  $path = "$work/$name.txt"
  [IO.File]::WriteAllText((Join-Path $repo $path), (($text -replace '\|', "`n")), $enc)
  return $path
}
$c = $Crop -split ":"
$cw = [int]$c[0]; $ch = [int]$c[1]
$vw = [int]([Math]::Round($cw * $VideoHeight / $ch / 2) * 2)
$px = 30 + $vw + 50                       # caption panel x
$bg = "0x0F1317"
$codec = @("-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", "-r", "$Fps", "-an")
$label = TextFile "label" "Meta XR Simulator footage (Quest 3 profile)|Real app build, live AI responses."
$brand = TextFile "brand" "CURSIVIS SPATIAL"
$tagline = TextFile "tagline" "Selection is Context. Gesture is Intent.|Cursivis is Action."
$parts = New-Object System.Collections.Generic.List[string]

# Title card
$tt = TextFile "title_name" "Cursivis Spatial"
$tg = TextFile "title_tag" "Selection is Context. Gesture is Intent. Cursivis is Action."
$tl = TextFile "title_label" "Hands-first AI for Meta Quest 3 / 3S  ·  Meta XR Simulator footage, live AI responses"
$vf = "drawtext=fontfile=${fontB}:textfile=${tt}:expansion=none:fontsize=104:fontcolor=white:x=(w-text_w)/2:y=390," +
      "drawtext=fontfile=${fontS}:textfile=${tg}:expansion=none:fontsize=40:fontcolor=0xC9D0D8:x=(w-text_w)/2:y=540," +
      "drawtext=fontfile=${fontR}:textfile=${tl}:expansion=none:fontsize=28:fontcolor=0x8A929B:x=(w-text_w)/2:y=640"
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=${bg}:s=1920x1080:r=${Fps}:d=3.5" -vf $vf @codec "$work/p00.mp4"
$parts.Add("p00.mp4")

$rows = @(Get-Content $Segments -Encoding UTF8 | Where-Object { $_ -and -not $_.StartsWith("#") })
$total = 3.5; $i = 0
foreach ($row in $rows) {
  $i++
  $col = $row -split "`t"
  $s = [double]::Parse($col[0], $inv)
  $freeze = $col[1].StartsWith("+")
  $dur = if ($freeze) { [double]::Parse($col[1].Substring(1), $inv) } else { [double]::Parse($col[1], $inv) - $s }
  $total += $dur
  $title = TextFile ("t{0:00}" -f $i) $col[2]
  $sub = TextFile ("s{0:00}" -f $i) ($(if ($col.Count -gt 3) { $col[3] } else { "" }))
  $note = TextFile ("o{0:00}" -f $i) ($(if ($col.Count -gt 4) { $col[4] } else { "" }))
  $ss = $s.ToString($inv); $dd = $dur.ToString("0.###", $inv)
  $fc = "[0:v]crop=${Crop},scale=${vw}:${VideoHeight}:flags=lanczos,setsar=1[v];[1:v][v]overlay=30:40:shortest=1," +
        "drawtext=fontfile=${fontR}:textfile=${label}:expansion=none:fontsize=24:fontcolor=0x8A929B:x=${px}:y=56:line_spacing=10," +
        "drawtext=fontfile=${fontS}:textfile=${title}:expansion=none:fontsize=48:fontcolor=white:x=${px}:y=300:line_spacing=14," +
        "drawtext=fontfile=${fontR}:textfile=${sub}:expansion=none:fontsize=30:fontcolor=0xC9D0D8:x=${px}:y=440:line_spacing=14," +
        "drawtext=fontfile=${fontR}:textfile=${note}:expansion=none:fontsize=23:fontcolor=0x8A929B:x=${px}:y=820:line_spacing=10," +
        "drawtext=fontfile=${fontB}:textfile=${brand}:expansion=none:fontsize=28:fontcolor=0xD5DAE0:x=${px}:y=950," +
        "drawtext=fontfile=${fontR}:textfile=${tagline}:expansion=none:fontsize=20:fontcolor=0x8A929B:x=${px}:y=990:line_spacing=6[out]"
  $partName = "p{0:00}.mp4" -f $i
  if ($freeze) {
    $still = "$work/still{0:00}.png" -f $i
    & ffmpeg -hide_banner -loglevel error -y -ss $ss -i $Raw -frames:v 1 -update 1 $still
    & ffmpeg -hide_banner -loglevel error -y -loop 1 -framerate $Fps -t $dd -i $still -f lavfi -i "color=c=${bg}:s=1920x1080:r=${Fps}" -filter_complex $fc -map "[out]" -t $dd @codec "$work/$partName"
  } else {
    & ffmpeg -hide_banner -loglevel error -y -ss $ss -t $dd -i $Raw -f lavfi -i "color=c=${bg}:s=1920x1080:r=${Fps}" -filter_complex $fc -map "[out]" @codec "$work/$partName"
  }
  $parts.Add($partName)
}

# End card
$e1 = TextFile "end_name" "Cursivis Spatial"
$e2 = TextFile "end_line" "Select anything. Cursivis understands what you might want to do next."
$e3 = TextFile "end_tech" "Meta Quest 3 / 3S  ·  Unity 6  ·  Meta XR Core, Interaction SDK, MR Utility Kit 209|Passthrough Camera API  ·  Gemini through a backend; no keys on the headset"
$e4 = TextFile "end_repo" "github.com/UnknownGod2011/Cursivis-spatial"
$e5 = TextFile "end_note" "Recorded in Meta XR Simulator without a headset. Some steps were triggered by the app's test autopilot;|questions were typed by it because the simulator has no microphone. On-device behaviour is not yet verified."
$vf = "drawtext=fontfile=${fontB}:textfile=${e1}:expansion=none:fontsize=88:fontcolor=white:x=(w-text_w)/2:y=290," +
      "drawtext=fontfile=${fontS}:textfile=${e2}:expansion=none:fontsize=38:fontcolor=0xC9D0D8:x=(w-text_w)/2:y=430," +
      "drawtext=fontfile=${fontR}:textfile=${e3}:expansion=none:fontsize=28:fontcolor=0x9AA2AB:x=(w-text_w)/2:y=540:line_spacing=14," +
      "drawtext=fontfile=${fontS}:textfile=${e4}:expansion=none:fontsize=32:fontcolor=0x5FD3F3:x=(w-text_w)/2:y=670," +
      "drawtext=fontfile=${fontR}:textfile=${e5}:expansion=none:fontsize=24:fontcolor=0x8A929B:x=(w-text_w)/2:y=780:line_spacing=10"
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=${bg}:s=1920x1080:r=${Fps}:d=7" -vf $vf @codec "$work/p99.mp4"
$parts.Add("p99.mp4")
$total += 7

$list = ($parts | ForEach-Object { "file '$_'" }) -join "`n"
[IO.File]::WriteAllText((Join-Path $repo "$work/list.txt"), $list, $enc)
$outDir = Split-Path -Parent $Out
if ($outDir) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
& ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i "$work/list.txt" -c copy -movflags +faststart $Out
$d = & ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $Out
Write-Output ("{0}: {1:n1} s (planned {2:n1} s), {3:n1} MB" -f $Out, [double]::Parse($d, $inv), $total, ((Get-Item $Out).Length / 1MB))
if ([double]::Parse($d, $inv) -ge 180) { Write-Warning "Video is 3:00 or longer; the competition requires under 3 minutes." }
