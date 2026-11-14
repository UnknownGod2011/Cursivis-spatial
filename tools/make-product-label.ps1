<#
.SYNOPSIS
  Renders the workspace product image: the back of an unbranded energy drink can (ingredients, allergy and caffeine
  warnings, nutrition table). Original artwork, no brand names or logos (competition rules 4.19).
.EXAMPLE
  ./tools/make-product-label.ps1
#>
param([string]$Out = "unity/CursivisSpatial/Assets/Cursivis/Resources/Workspace/energy-drink-back.png")
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
Add-Type -AssemblyName System.Drawing
$W = 1600; $H = 880
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = "AntiAlias"; $g.TextRenderingHint = "AntiAliasGridFit"; $g.InterpolationMode = "HighQualityBicubic"

# Aluminium can body: cylinder shading left-to-right with a soft specular band
$blend = New-Object System.Drawing.Drawing2D.ColorBlend 6
$blend.Colors = @(
  [System.Drawing.Color]::FromArgb(255, 6, 14, 26), [System.Drawing.Color]::FromArgb(255, 18, 40, 70),
  [System.Drawing.Color]::FromArgb(255, 44, 78, 120), [System.Drawing.Color]::FromArgb(255, 28, 58, 96),
  [System.Drawing.Color]::FromArgb(255, 14, 32, 58), [System.Drawing.Color]::FromArgb(255, 4, 10, 20))
$blend.Positions = @(0.0, 0.18, 0.42, 0.62, 0.86, 1.0)
$body = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle 0, 0, $W, $H), [System.Drawing.Color]::Black, [System.Drawing.Color]::Black, 0.0)
$body.InterpolationColors = $blend
$g.FillRectangle($body, 0, 0, $W, $H)
$spec = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle 560, 0, 220, $H), [System.Drawing.Color]::FromArgb(0, 255, 255, 255), [System.Drawing.Color]::FromArgb(0, 255, 255, 255), 0.0)
$sb = New-Object System.Drawing.Drawing2D.ColorBlend 3
$sb.Colors = @([System.Drawing.Color]::FromArgb(0, 255, 255, 255), [System.Drawing.Color]::FromArgb(38, 220, 235, 255), [System.Drawing.Color]::FromArgb(0, 255, 255, 255))
$sb.Positions = @(0.0, 0.5, 1.0)
$spec.InterpolationColors = $sb
$g.FillRectangle($spec, 560, 0, 220, $H)
# rim shadows top and bottom (photo of a can, slightly cropped)
$rim = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle 0, 0, $W, 40), [System.Drawing.Color]::FromArgb(160, 0, 0, 0), [System.Drawing.Color]::FromArgb(0, 0, 0, 0), 90.0)
$g.FillRectangle($rim, 0, 0, $W, 40)
$rim2 = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle 0, ($H - 40), $W, 40), [System.Drawing.Color]::FromArgb(0, 0, 0, 0), [System.Drawing.Color]::FromArgb(170, 0, 0, 0), 90.0)
$g.FillRectangle($rim2, 0, ($H - 40), $W, 40)

$white = [System.Drawing.Brushes]::White
$soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 222, 230, 240))
$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 196, 61))
$fH = New-Object System.Drawing.Font("Arial", 30, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fB = New-Object System.Drawing.Font("Arial", 27, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fBb = New-Object System.Drawing.Font("Arial", 27, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fS = New-Object System.Drawing.Font("Arial", 23, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fT = New-Object System.Drawing.Font("Arial", 24, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fTb = New-Object System.Drawing.Font("Arial", 24, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

# Left column: product line, ingredients, allergy and caffeine advice
$lx = 70; $lw = 720
$g.DrawString("SPARKLING ENERGY DRINK  ·  TROPICAL FLAVOUR", $fH, $accent, $lx, 62)
$ingr = "INGREDIENTS: Carbonated water, sugar, acid (citric acid), taurine (0.4%), acidity regulators (sodium citrates, magnesium carbonate), caffeine (0.03%), flavourings, colour (E120), vitamins (niacin, pantothenic acid, B6, B12, D3), inositol, glucuronolactone."
$g.DrawString($ingr, $fB, $white, (New-Object System.Drawing.RectangleF $lx, 128, $lw, 300))
$g.DrawString("ALLERGY ADVICE: produced in a factory that also handles milk.", $fBb, $soft, (New-Object System.Drawing.RectangleF $lx, 430, $lw, 80))
$g.DrawString("HIGH CAFFEINE CONTENT (32 mg/100 ml). Not recommended for children or pregnant or breast-feeding women. Do not mix with alcohol.", $fB, $white, (New-Object System.Drawing.RectangleF $lx, 516, $lw, 130))
$g.DrawString("Best before: see base.   Store in a cool, dry place.   250 ml ℮", $fS, $soft, $lx, 676)
$g.DrawString("Serve chilled. Once opened, consume immediately.", $fS, $soft, $lx, 712)

# Right column: nutrition table
$tx = 870; $tw = 660; $ty = 64
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 235, 240, 248)), 2
$thin = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 235, 240, 248)), 1
$g.DrawRectangle($pen, $tx, $ty, $tw, 640)
$g.DrawString("NUTRITION INFORMATION", $fH, $white, ($tx + 18), ($ty + 14))
$g.DrawString("Typical values", $fTb, $white, ($tx + 18), ($ty + 66))
$g.DrawString("per 100 ml", $fTb, $white, ($tx + 330), ($ty + 66))
$g.DrawString("per can (250 ml)", $fTb, $white, ($tx + 470), ($ty + 66))
$rows = @(
  @("Energy", "192 kJ / 46 kcal", "480 kJ / 115 kcal"),
  @("Fat", "0 g", "0 g"),
  @("  of which saturates", "0 g", "0 g"),
  @("Carbohydrate", "11 g", "27.5 g"),
  @("  of which sugars", "11 g", "27.5 g"),
  @("Protein", "0 g", "0 g"),
  @("Salt", "0.10 g", "0.25 g"),
  @("Niacin", "3.2 mg (20%*)", "8.0 mg (50%*)"),
  @("Vitamin B6", "0.4 mg (29%*)", "1.0 mg (71%*)"),
  @("Vitamin B12", "0.4 µg (16%*)", "1.0 µg (40%*)"),
  @("Vitamin D", "0.5 µg (10%*)", "1.25 µg (25%*)"),
  @("Caffeine", "32 mg", "80 mg"))
$y = $ty + 106
foreach ($r in $rows) {
  $g.DrawLine($thin, ($tx + 14), $y, ($tx + $tw - 14), $y)
  $font = if ($r[0] -match '^(Energy|Carbohydrate|Caffeine)$') { $fTb } else { $fT }
  $g.DrawString($r[0], $font, $white, ($tx + 18), ($y + 6))
  $g.DrawString($r[1], $fT, $white, ($tx + 300), ($y + 6))
  $g.DrawString($r[2], $fT, $white, ($tx + 470), ($y + 6))
  $y += 41
}
$g.DrawString("*Reference intake of an average adult", $fS, $soft, ($tx + 18), ($y + 8))

# Generic barcode (not a real product code) and recycling note
$rnd = New-Object System.Random 42
$bx = 1180; $by = 742
$g.FillRectangle([System.Drawing.Brushes]::White, ($bx - 14), ($by - 10), 330, 116)
$x = $bx
while ($x -lt $bx + 300) { $wbar = $rnd.Next(1, 5); if ($rnd.Next(0, 2) -eq 1) { $g.FillRectangle([System.Drawing.Brushes]::Black, $x, $by, $wbar, 74) }; $x += $wbar + 1 }
$g.DrawString("2 300000 481516", $fS, [System.Drawing.Brushes]::Black, ($bx + 60), ($by + 76))
$g.DrawString("Please recycle this can", $fS, $soft, 870, 770)

$g.Dispose()
$dir = Split-Path -Parent $Out
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved $Out (${W}x${H})"
