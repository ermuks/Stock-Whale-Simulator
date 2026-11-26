# Bakes the UI pictogram atlas (white shapes on transparent, 8 x 8 cells of 128 px).
# Colours are applied in Unity (Image.color), so every shape here is pure white.
# Cell layout must match Assets/Scripts/NightShift/UiPictograms.cs.
#   0-9   sector pictograms (stroke)   insurance, ai, food, bio, display, finance, electronics, semiconductor, ev, tech
#   10-15 operation symbols (stroke)   ignite, unload, slam, close, wash, wall
#   16-25 logo bases (filled), same sector order
#   26    ring (avatar mood ring)      27-32 avatar shapes: circle, square, triangle, hexagon, diamond, half disc
#   33    small dot                    34 bar
#   35-38 message marks: system, analysis, leading room, proof
#   39    cut square (logo knock-out)  40 cut dot  41 cut slash
#   42-47 operation symbols, filled-weight variants for small sizes (same order as 10-15)
# Usage: powershell -ExecutionPolicy Bypass -File Tools/UiArt/BakePictograms.ps1
param([string]$Out = "$PSScriptRoot\..\..\Assets\Resources\UI\Pictograms\Pictograms.png")
Add-Type -AssemblyName System.Drawing
$cell = 128; $cols = 8
$bmp = New-Object System.Drawing.Bitmap ($cell * $cols), ($cell * $cols), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)
$white = [System.Drawing.Color]::White

function Begin-Cell([int]$index, [double]$grid, [double]$fill = 0.84) {
    $g.ResetTransform()
    $x = ($index % $cols) * $cell; $y = [math]::Floor($index / $cols) * $cell
    $s = $cell * $fill / $grid; $pad = ($cell - $grid * $s) / 2
    $g.TranslateTransform([single]($x + $pad), [single]($y + $pad)); $g.ScaleTransform([single]$s, [single]$s)
}
function New-Pen([double]$w) { $p = New-Object System.Drawing.Pen $white, ([single]$w); $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'; return $p }
function Pts([double[]]$v) { $list = @(); for ($i = 0; $i -lt $v.Length; $i += 2) { $list += New-Object System.Drawing.PointF ([single]$v[$i]), ([single]$v[$i + 1]) }; return ,([System.Drawing.PointF[]]$list) }
function Line($pen, [double[]]$v) { $g.DrawLines($pen, (Pts $v)) }
function Poly($pen, [double[]]$v) { $g.DrawPolygon($pen, (Pts $v)) }
function FillPoly([double[]]$v) { $g.FillPolygon((New-Object System.Drawing.SolidBrush $white), (Pts $v)) }
function Circle($pen, $cx, $cy, $r) { $g.DrawEllipse($pen, [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r)) }
function Disc($cx, $cy, $r, [int]$alpha = 255) { $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255))), [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r)) }
function Box($pen, $x, $y, $w, $h) { $g.DrawRectangle($pen, [single]$x, [single]$y, [single]$w, [single]$h) }
function FillBox($x, $y, $w, $h, [int]$alpha = 255) { $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255))), [single]$x, [single]$y, [single]$w, [single]$h) }
function Bez($pen, [double[]]$v) { $g.DrawBeziers($pen, (Pts $v)) }
function Arc($pen, $cx, $cy, $r, $start, $sweep) { $g.DrawArc($pen, [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r), [single]$start, [single]$sweep) }

# ── 0-9 sector pictograms (24 grid) ──
$p = New-Pen 1.7
Begin-Cell 0 24; $path = New-Object System.Drawing.Drawing2D.GraphicsPath; $path.AddLine(12,3,19,6); $path.AddLine(19,6,19,11); $path.AddBezier(19,11,19,16,15.8,19.4,12,21); $path.AddBezier(12,21,8.2,19.4,5,16,5,11); $path.AddLine(5,11,5,6); $path.CloseFigure(); $g.DrawPath($p, $path); Line $p @(8.5,12, 11,14.5, 15.5,9.5)
Begin-Cell 1 24; Circle $p 6 7 2.3; Circle $p 18 7 2.3; Circle $p 12 18 2.3; Line $p @(8.3,7, 15.7,7); Line $p @(7.2,9, 10.9,16); Line $p @(16.8,9, 13.1,16)
Begin-Cell 2 24; Line $p @(3.5,12, 20.5,12); Arc $p 12 12 8.5 0 180; Bez $p @(9,3.5, 7.8,5, 10.2,6.2, 9,8); Bez $p @(13,3, 11.8,4.5, 14.2,5.7, 13,7.5); Bez $p @(17,3.5, 15.8,5, 18.2,6.2, 17,8)
Begin-Cell 3 24; Bez $p @(7,3, 7,9, 17,9, 17,15, 17,18.5, 7,18.5, 7,21); Bez $p @(17,3, 17,9, 7,9, 7,15, 7,18.5, 17,18.5, 17,21); Line $p @(8.6,6, 15.4,6); Line $p @(8.6,18, 15.4,18)
Begin-Cell 4 24; Box $p 3 4 18 12; Line $p @(9,20, 15,20); Line $p @(12,16, 12,20)
Begin-Cell 5 24; Line $p @(3,9, 12,4, 21,9); Line $p @(4,20, 20,20); foreach ($x in 6,10,14,18) { Line $p @($x,10, $x,18) }
Begin-Cell 6 24; Box $p 6 6 12 12; foreach ($x in 9,12,15) { Line $p @($x,3, $x,6); Line $p @($x,18, $x,21) }; foreach ($y in 9,15) { Line $p @(3,$y, 6,$y); Line $p @(18,$y, 21,$y) }
Begin-Cell 7 24; Circle $p 12 12 8.5; $q = New-Pen 1.3; Box $q 8 8 8 8; Line $q @(12,8, 12,16); Line $q @(8,12, 16,12)
Begin-Cell 8 24; Poly $p @(4,15, 6,10, 18,10, 20,15, 20,18, 4,18); Disc 8 18 1.8; Disc 16 18 1.8; Line $p @(12.5,2.5, 10,6.5, 13,6.5, 10.5,10)
Begin-Cell 9 24; Poly $p @(12,3, 19.8,7.5, 19.8,16.5, 12,21, 4.2,16.5, 4.2,7.5); Line $p @(9.5,9.5, 7.2,12, 9.5,14.5); Line $p @(14.5,9.5, 16.8,12, 14.5,14.5)

# ── 10-15 operation symbols (32 grid) and 42-47 heavier variants ──
function Draw-Ops([int]$first, [double]$w) {
    $o = New-Pen $w
    Begin-Cell ($first + 0) 32; FillBox 5 21 4 5 90; FillBox 12 17 4 9 90; FillBox 19 12 4 14 90; Line $o @(6,19, 13,13, 18,16, 27,6); Line $o @(21,6, 27,6, 27,12)
    Begin-Cell ($first + 1) 32; Line $o @(4,8, 10,8, 10,12, 16,12, 16,16, 22,16, 22,20, 28,20); Line $o @(4,26, 22,26); Line $o @(18,22, 22,26, 18,30)
    Begin-Cell ($first + 2) 32; Line $o @(16,4, 16,20); Line $o @(9,14, 16,21, 23,14); $h = New-Pen ($w * 1.25); Line $h @(4,27, 28,27); Line $o @(8,23.5, 5.5,26); Line $o @(24,23.5, 26.5,26)
    Begin-Cell ($first + 3) 32; Arc $o 16 16 11 270 -270; Line $o @(16,9.5, 16,16, 20,19); Line $o @(21.5,4.5, 27.5,4.5, 27.5,10.5)
    Begin-Cell ($first + 4) 32; Arc $o 16 16 10 200 120; Line $o @(23.7,4.6, 23.7,9.6, 18.7,9.6); Arc $o 16 16 10 20 120; Line $o @(8.3,27.4, 8.3,22.4, 13.3,22.4); Line $o @(13,19, 13,15); Line $o @(16,19, 16,13); Line $o @(19,19, 19,16)
    Begin-Cell ($first + 5) 32; $d = New-Pen $w; $d.DashStyle = 'Dash'; Line $d @(4,8.5, 28,8.5); $b = New-Pen ($w * 0.8); Box $b 4 14 11 5; Box $b 17 14 11 5; Box $b 4 21 6 5; Box $b 12 21 10 5; Box $b 24 21 4 5
}
Draw-Ops 10 2.2
Draw-Ops 42 3.0

# ── 16-25 logo bases (44 grid, filled) ──
$solid = New-Object System.Drawing.SolidBrush $white
Begin-Cell 16 44 0.9; $path = New-Object System.Drawing.Drawing2D.GraphicsPath; $path.AddLine(22,4,37,10); $path.AddLine(37,10,37,20); $path.AddBezier(37,20,37,30,30,37,22,40); $path.AddBezier(22,40,14,37,7,30,7,20); $path.AddLine(7,20,7,10); $path.CloseFigure(); $g.FillPath($solid, $path)
Begin-Cell 17 44 0.9; $t = New-Pen 4.5; Line $t @(11,13, 22,32, 33,13, 11,13); Disc 11 13 7; Disc 33 13 7; Disc 22 32 7
Begin-Cell 18 44 0.9; $g.FillPie($solid, 5, 4, 34, 34, 0, 180); FillBox 5 20 34 3
Begin-Cell 19 44 0.9; $t = New-Pen 6; Bez $t @(13,4, 13,16, 31,16, 31,22, 31,28, 13,28, 13,40); Bez $t @(31,4, 31,16, 13,16, 13,22, 13,28, 31,28, 31,40)
Begin-Cell 20 44 0.9; FillBox 4 8 36 22; FillBox 17 32 10 5
Begin-Cell 21 44 0.9; FillPoly @(3,14, 22,3, 41,14); foreach ($x in 7,19.5,32) { FillBox $x 17 5.5 18 }; FillBox 3 37 38 4
Begin-Cell 22 44 0.9; FillBox 10 10 24 24; foreach ($v in 14,21,28) { FillBox ($v - 1.5) 4 3 5; FillBox ($v - 1.5) 35 3 5; FillBox 4 ($v - 1.5) 5 3; FillBox 35 ($v - 1.5) 5 3 }
Begin-Cell 23 44 0.9; Disc 22 22 18
Begin-Cell 24 44 0.9; FillPoly @(26,2, 9,25, 20,25, 16,42, 35,17, 24,17)
Begin-Cell 25 44 0.9; FillPoly @(22,3, 39,12.5, 39,31.5, 22,41, 5,31.5, 5,12.5)

# ── 26-34 avatar parts and helpers (32 grid) ──
Begin-Cell 26 32 0.96; $r = New-Pen 2.6; Circle $r 16 16 14.3
Begin-Cell 27 32; Disc 16 16 13
Begin-Cell 28 32; FillBox 4 4 24 24
Begin-Cell 29 32; FillPoly @(16,3, 29,27, 3,27)
Begin-Cell 30 32; FillPoly @(16,2.5, 27.7,9.2, 27.7,22.8, 16,29.5, 4.3,22.8, 4.3,9.2)
Begin-Cell 31 32; FillPoly @(16,2, 30,16, 16,30, 2,16)
Begin-Cell 32 32; $g.FillPie($solid, 3, 3, 26, 26, 180, 180)
Begin-Cell 33 32; Disc 16 16 6
Begin-Cell 34 32; FillBox 3 13 26 6

# ── 35-38 message marks (24 grid) ──
$m = New-Pen 1.8
Begin-Cell 35 24; Disc 12 12 2.6; Circle $m 12 12 7
Begin-Cell 36 24; Arc $m 12 13 8 180 180; Line $m @(12,13, 16.5,8); Line $m @(4,17, 20,17)
Begin-Cell 37 24; Line $m @(6,21, 6,3); FillPoly @(6,3, 19,6.5, 6,10.5)
Begin-Cell 38 24; Box $m 4 4 16 16; Line $m @(8,12.5, 11,15.5, 16.5,8.5)

# ── 39-41 knock-out cuts (44 grid) ──
Begin-Cell 39 44 0.9; FillBox 14 14 16 16
Begin-Cell 40 44 0.9; Disc 22 22 7
Begin-Cell 41 44 0.9; FillPoly @(4,36, 32,4, 40,8, 12,40)

$g.Dispose()
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"baked $Out"
