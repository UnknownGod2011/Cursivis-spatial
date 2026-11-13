<#
  Renders original, unbranded fixture images used for backend live tests, the Cursivis workspace
  and the simulator test room: a food-package back label, a printed French notice, a handwritten note.
  Uses GDI+ (System.Drawing) only. Output: backend/tests/fixtures/*.png
#>
param([string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "backend/tests/fixtures"))
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function New-Canvas([int]$w, [int]$h, [System.Drawing.Color]$bg) {
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = "AntiAlias"; $g.TextRenderingHint = "AntiAliasGridFit"; $g.InterpolationMode = "HighQualityBicubic"
  $g.Clear($bg)
  return @($bmp, $g)
}
function Brush($hex) { New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex)) }
function Font($name, $size, $style = "Regular") { New-Object System.Drawing.Font($name, [float]$size, [System.Drawing.FontStyle]$style, [System.Drawing.GraphicsUnit]::Pixel) }
function Draw-Wrapped($g, $text, $font, $brush, $x, $y, $w) {
  $rect = New-Object System.Drawing.RectangleF ([float]$x), ([float]$y), ([float]$w), 4000
  $fmt = New-Object System.Drawing.StringFormat
  $g.DrawString($text, $font, $brush, $rect, $fmt)
  return $g.MeasureString($text, $font, [int]$w, $fmt).Height
}
# Draws rich text runs with word wrapping; runs = @(@{t="text"; b=$true}, ...)
function Draw-Runs($g, $runs, $fontName, $size, $brush, $x, $y, $w) {
  $regular = Font $fontName $size "Regular"; $bold = Font $fontName $size "Bold"
  $cx = $x; $cy = $y; $lineH = $size * 1.32
  $space = $g.MeasureString("a a", $regular).Width - $g.MeasureString("aa", $regular).Width
  foreach ($run in $runs) {
    $f = if ($run.b) { $bold } else { $regular }
    foreach ($word in ($run.t -split " ")) {
      if ($word -eq "") { continue }
      $ww = $g.MeasureString($word, $f, 2000, [System.Drawing.StringFormat]::GenericTypographic).Width
      if ($word -match "^[\)\],.;:]" -and $cx -gt $x) { $cx -= $space }
      if ($cx + $ww -gt $x + $w) { $cx = $x; $cy += $lineH }
      $g.DrawString($word, $f, $brush, [float]$cx, [float]$cy, [System.Drawing.StringFormat]::GenericTypographic)
      $cx += $ww + $space
    }
  }
  return ($cy + $lineH - $y)
}

# ---------- 1. Food package back label ----------
$res = New-Canvas 1400 1000 ([System.Drawing.Color]::FromArgb(255, 252, 249, 242)); $bmp = $res[0]; $g = $res[1]
$ink = Brush "#1f1a17"; $muted = Brush "#5a524b"; $accent = Brush "#7a3b12"
$g.FillRectangle((Brush "#7a3b12"), 0, 0, 1400, 18)
$g.DrawString("HAZELNUT COCOA WAFER BARS", (Font "Arial" 44 "Bold"), $accent, 60, 50)
$g.DrawString("Crispy wafer layers with hazelnut cocoa cream  ·  6 bars × 22 g (132 g)", (Font "Arial" 24), $muted, 62, 108)
$y = 170
$h = Draw-Runs $g @(
  @{t="INGREDIENTS:"; b=$true}, @{t="Sugar,"}, @{t="wheat"; b=$true}, @{t="flour, palm oil,"}, @{t="hazelnuts"; b=$true}, @{t="(13%), skimmed"},
  @{t="milk"; b=$true}, @{t="powder,"}, @{t="whey"; b=$true}, @{t="powder ("}, @{t="milk"; b=$true}, @{t="), fat-reduced cocoa powder (7%), emulsifier:"},
  @{t="soy"; b=$true}, @{t="lecithin (E322), raising agent: sodium bicarbonate (E500), glucose syrup, salt, natural vanilla flavouring."}
) "Arial" 30 $ink 60 $y 820
$y += $h + 26
$h = Draw-Runs $g @(@{t="ALLERGY ADVICE:"; b=$true}, @{t="For allergens, including cereals containing gluten, see ingredients in"}, @{t="bold."; b=$true}, @{t="May contain peanuts and other nuts. Not suitable for people with a milk allergy."}) "Arial" 27 $ink 60 $y 820
$y += $h + 26
$g.DrawString("Store in a cool, dry place. Best before: see top of pack.", (Font "Arial" 25), $muted, 60, $y)
$g.DrawString("Produced in the EU for: Northfield Snack Co., 12 Mill Lane, Leeds LS1 4AB", (Font "Arial" 22), $muted, 60, $y + 40)
# Nutrition table
$tx = 930; $ty = 170; $tw = 410
$pen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml("#1f1a17")), 3
$g.DrawRectangle($pen, $tx, $ty, $tw, 640)
$g.DrawString("NUTRITION", (Font "Arial" 30 "Bold"), $ink, $tx + 18, $ty + 14)
$g.DrawString("Typical values", (Font "Arial" 20 "Bold"), $ink, $tx + 18, $ty + 62)
$g.DrawString("100 g", (Font "Arial" 20 "Bold"), $ink, $tx + 205, $ty + 62)
$g.DrawString("1 bar (22 g)", (Font "Arial" 20 "Bold"), $ink, $tx + 290, $ty + 62)
$rows = @(
  @("Energy", "2210 kJ", "486 kJ"), @("", "529 kcal", "116 kcal"), @("Fat", "31 g", "6.8 g"), @("of which saturates", "14 g", "3.1 g"),
  @("Carbohydrate", "55 g", "12 g"), @("of which sugars", "40 g", "8.8 g"), @("Fibre", "2.5 g", "0.6 g"), @("Protein", "7.1 g", "1.6 g"), @("Salt", "0.31 g", "0.07 g")
)
$ry = $ty + 104
foreach ($r in $rows) {
  $g.DrawLine((New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml("#8c8279")), 1), $tx + 12, $ry - 6, $tx + $tw - 12, $ry - 6)
  $g.DrawString($r[0], (Font "Arial" 21), $ink, $tx + 18, $ry)
  $g.DrawString($r[1], (Font "Arial" 21), $ink, $tx + 205, $ry)
  $g.DrawString($r[2], (Font "Arial" 21), $ink, $tx + 300, $ry)
  $ry += 56
}
$g.DrawString("This pack contains 6 portions", (Font "Arial" 19), $muted, $tx + 18, $ty + 600)
# Barcode-like stripes (decorative, not a real code)
$bx = 60; for ($i = 0; $i -lt 46; $i++) { $wbar = (($i * 7) % 3) + 1; $g.FillRectangle($ink, $bx, 880, $wbar * 2, 80); $bx += $wbar * 2 + 3 }
$g.DrawString("RECYCLE: wrapper - not yet recycled  ·  box - widely recycled", (Font "Arial" 20), $muted, 470, 915)
$bmp.Save((Join-Path $OutDir "food-label.png"), [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()

# ---------- 2. Printed French notice ----------
$res = New-Canvas 1300 900 ([System.Drawing.Color]::FromArgb(255, 250, 250, 247)); $bmp = $res[0]; $g = $res[1]
$g.DrawString("Bibliothèque municipale — Avis aux usagers", (Font "Georgia" 42 "Bold"), $ink, 70, 60)
$g.DrawLine((New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml("#3a3a3a")), 2), 70, 125, 1230, 125)
$fr = "À partir du lundi 2 décembre, la bibliothèque sera ouverte du mardi au samedi, de 9 h à 19 h. Les ouvrages empruntés doivent être rendus dans un délai de trois semaines ; une prolongation est possible une seule fois, en ligne ou à l'accueil. Les salles de travail du deuxième étage sont réservées aux étudiants pendant la période des examens. Merci de garder le silence, de ne pas manger dans les salles de lecture et de mettre vos téléphones en mode silencieux. Pour toute question, adressez-vous au personnel d'accueil ou écrivez-nous à l'adresse indiquée sur notre site."
Draw-Wrapped $g $fr (Font "Georgia" 34) $ink 70 160 1160 | Out-Null
$g.DrawString("La Direction", (Font "Georgia" 30 "Italic"), $muted, 1000, 800)
$bmp.Save((Join-Path $OutDir "printed-french.png"), [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()

# ---------- 3. Handwritten note (sticky note) ----------
$res = New-Canvas 900 900 ([System.Drawing.Color]::FromArgb(255, 255, 236, 140)); $bmp = $res[0]; $g = $res[1]
$hand = if ((New-Object System.Drawing.Text.InstalledFontCollection).Families.Name -contains "Ink Free") { "Ink Free" } else { "Segoe Print" }
$pencil = Brush "#1d2a5c"
$g.DrawString("Thursday", (Font $hand 54 "Bold"), $pencil, 60, 50)
$lines = @("- call plumber before Fri (kitchen sink)", "- send Q3 deck to Maya + ask re budget", "- book dentist, mornings only", "- buy oat milk, lemons, rice", "- renew library books by the 3rd!", "- gym 7pm?")
$ly = 170; foreach ($l in $lines) { $g.DrawString($l, (Font $hand 40), $pencil, 60, $ly); $ly += 92 }
$bmp.Save((Join-Path $OutDir "handwritten-note.png"), [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()


# ---------- 4. Crop: nutrition table only (no ingredients visible) ----------
$src = [System.Drawing.Bitmap]::FromFile((Join-Path $OutDir "food-label.png"))
$crop = $src.Clone((New-Object System.Drawing.Rectangle 920, 160, 440, 660), $src.PixelFormat)
$crop.Save((Join-Path $OutDir "food-label-nutrition-only.png"), [System.Drawing.Imaging.ImageFormat]::Png); $crop.Dispose(); $src.Dispose()
Get-ChildItem $OutDir -Filter *.png | ForEach-Object { "{0} {1}KB" -f $_.Name, [math]::Round($_.Length / 1KB) }
