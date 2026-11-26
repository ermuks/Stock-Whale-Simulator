# Bakes the information-carrying images (not menu icons):
#   Logos.png   - one designed logo per listed company, 4 x 4 cells of 256 px (order = $Logos below; must match CompanyLogos.cs)
#   Emblems.png - one large symbol graphic per operation, 2 x 4 cells of 512 x 256 px, white on transparent (tinted in Unity)
#                 order: ignite, unload, slam, close, wash, wall
# Usage: powershell -ExecutionPolicy Bypass -File Tools/UiArt/BakeBrand.ps1
param([string]$Folder = "$PSScriptRoot\..\..\Assets\Resources\UI\Brand")
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Folder | Out-Null
function Col([string]$hex, [int]$a = 255) { $c = [System.Drawing.ColorTranslator]::FromHtml($hex); return [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B) }
function Pts([double[]]$v) { $list = @(); for ($i = 0; $i -lt $v.Length; $i += 2) { $list += New-Object System.Drawing.PointF ([single]$v[$i]), ([single]$v[$i + 1]) }; return ,([System.Drawing.PointF[]]$list) }
function Brush($color) { return New-Object System.Drawing.SolidBrush $color }
function Pen($color, [double]$w) { $p = New-Object System.Drawing.Pen $color, ([single]$w); $p.LineJoin = 'Miter'; $p.StartCap = 'Flat'; $p.EndCap = 'Flat'; return $p }

# ── Logos ── symbol, monogram, outer form, base colour, accent colour, accent kind
# Forms are straight-edged (square, hexagon, diamond, shield, cut-corner square, octagon, chevron tile) to stay with the terminal style.
$Logos = @(
  @('NM',  'N',  'shield',  '#1F4E79', '#5FA8E0', 'stripe'),   # 노보손해보험 · insurance
  @('DSA', 'DS', 'hex',     '#4B3AA8', '#A897FF', 'node'),     # 덕상가주에이아이 · AI
  @('HBF', 'HB', 'cut',     '#8A4B1C', '#F0A45C', 'bowl'),     # 두덕리식품 · food
  @('MBI', 'M',  'diamond', '#1E6B52', '#6FD3AC', 'helix'),    # 미래바이오 · bio
  @('DDP', 'LC', 'square',  '#24398F', '#7C95FF', 'split'),    # LC디스플레이 · display
  @('KMF', 'GD', 'octagon', '#6E5A1F', '#E2C66B', 'columns'),  # 굿데이금융 · finance
  @('GFD', 'G',  'circle',  '#2F6B2A', '#9BDB7A', 'leaf'),     # 그린푸드 · food
  @('CBT', 'CB', 'square',  '#5A2A6E', '#D28BF0', 'helix'),    # 코어바이오 · bio
  @('CED', 'CE', 'cut',     '#174F66', '#5EC8E8', 'pins'),     # 코어전자 · electronics
  @('NXT', 'NX', 'chevron', '#3B3F4A', '#2EE8CF', 'arrow'),    # 넥스트테크 · tech
  @('SSE', 'S',  'hex',     '#1B2F6B', '#6C8CFF', 'pins'),     # electronics
  @('SQH', 'SQ', 'octagon', '#4A2430', '#F07A8E', 'wafer'),    # semiconductor
  @('TZM', 'T',  'chevron', '#6B1E1E', '#F2665E', 'bolt'),     # EV
  @('AMV', 'AV', 'diamond', '#1D1D24', '#9FE870', 'wafer')     # semiconductor
)
$cell = 256
$bmp = New-Object System.Drawing.Bitmap 1024, 1024, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear([System.Drawing.Color]::Transparent)
function FormPath([string]$form) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  switch ($form) {
    'square'  { $p.AddRectangle((New-Object System.Drawing.RectangleF 16, 16, 224, 224)) }
    'circle'  { $p.AddEllipse(14, 14, 228, 228) }
    'hex'     { $p.AddPolygon((Pts @(128,10, 236,70, 236,186, 128,246, 20,186, 20,70))) }
    'diamond' { $p.AddPolygon((Pts @(128,8, 248,128, 128,248, 8,128))) }
    'octagon' { $p.AddPolygon((Pts @(84,14, 172,14, 242,84, 242,172, 172,242, 84,242, 14,172, 14,84))) }
    'cut'     { $p.AddPolygon((Pts @(16,16, 186,16, 240,70, 240,240, 70,240, 16,186))) }
    'shield'  { $p.AddLine(128,10,236,48); $p.AddLine(236,48,236,124); $p.AddBezier(236,124,236,190,190,228,128,248); $p.AddBezier(128,248,66,228,20,190,20,124); $p.AddLine(20,124,20,48); $p.CloseFigure() }
    'chevron' { $p.AddPolygon((Pts @(16,16, 240,16, 240,196, 128,244, 16,196))) }
  }
  return $p
}
$font = $null
foreach ($name in 'Segoe UI Black', 'Arial Black', 'Segoe UI') { try { $font = New-Object System.Drawing.Font $name, 10; if ($font.Name -eq $name) { break } } catch {} }
for ($i = 0; $i -lt $Logos.Count; $i++) {
  $l = $Logos[$i]; $x = ($i % 4) * $cell; $y = [math]::Floor($i / 4) * $cell
  $g.ResetTransform(); $g.TranslateTransform($x, $y)
  $base = Col $l[3]; $accent = Col $l[4]; $white = [System.Drawing.Color]::FromArgb(255, 245, 248, 250)
  $form = FormPath $l[2]
  $g.FillPath((Brush $base), $form)
  # accent: a sector-specific mark in the accent colour, clipped to the form so it reads as part of the logo
  $g.SetClip($form)
  switch ($l[5]) {
    'stripe'  { $g.FillPolygon((Brush $accent), (Pts @(0,170, 256,90, 256,128, 0,208))) }
    'node'    { foreach ($c in @(@(70,70),@(186,70),@(128,190))) { $g.FillEllipse((Brush $accent), [single]($c[0]-20), [single]($c[1]-20), 40, 40) }; $g.DrawPolygon((Pen $accent 10), (Pts @(70,70, 186,70, 128,190))) }
    'bowl'    { $g.FillPie((Brush $accent), 40, 120, 176, 150, 0, 180) }
    'helix'   { $q = Pen $accent 16; $g.DrawBezier($q, 60, 0, 60, 128, 196, 128, 196, 256); $g.DrawBezier($q, 196, 0, 196, 128, 60, 128, 60, 256) }
    'split'   { $g.FillRectangle((Brush $accent), 128, 0, 128, 256) }
    'columns' { foreach ($cx in 52, 112, 172) { $g.FillRectangle((Brush $accent), $cx, 150, 32, 110) } }
    'leaf'    { $g.FillPie((Brush $accent), 96, 96, 220, 220, 180, 90) }
    'pins'    { foreach ($k in 0..4) { $g.FillRectangle((Brush $accent), 20 + $k * 48, 214, 22, 42); $g.FillRectangle((Brush $accent), 20 + $k * 48, 0, 22, 42) } }
    'arrow'   { $g.FillPolygon((Brush $accent), (Pts @(150,40, 256,128, 150,216, 150,166, 96,166, 96,90, 150,90))) }
    'wafer'   { $g.DrawEllipse((Pen $accent 14), 150, 150, 160, 160); $g.DrawLine((Pen $accent 8), 150, 230, 256, 230); $g.DrawLine((Pen $accent 8), 230, 150, 230, 256) }
    'bolt'    { $g.FillPolygon((Brush $accent), (Pts @(200,0, 140,120, 186,120, 150,256, 256,96, 206,96, 240,0))) }
  }
  $g.ResetClip()
  # monogram: the company's initials, large and white, so the logo stays readable at 30 px
  $size = if ($l[1].Length -ge 2) { 92 } else { 120 }
  $f = New-Object System.Drawing.Font $font.FontFamily, $size, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
  $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
  $shadow = [System.Drawing.Color]::FromArgb(90, 0, 0, 0)
  $g.DrawString($l[1], $f, (Brush $shadow), (New-Object System.Drawing.RectangleF 4, 10, 256, 256), $fmt)
  $g.DrawString($l[1], $f, (Brush $white), (New-Object System.Drawing.RectangleF 0, 6, 256, 256), $fmt)
}
$g.Dispose(); $bmp.Save("$Folder\Logos.png", [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()

# ── Emblems ── 512 x 256 each, white line art on transparent. Drawn as small technical schematics, not decorations.
$bmp = New-Object System.Drawing.Bitmap 1024, 1024, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
$W = [System.Drawing.Color]::White
function Faint([int]$a) { return [System.Drawing.Color]::FromArgb($a, 255, 255, 255) }
function Cell([int]$i) { $g.ResetTransform(); $g.TranslateTransform(($i % 2) * 512, [math]::Floor($i / 2) * 256) }
function Candle($x, $open, $close, $high, $low, $a) { $p = Pen (Faint $a) 4; $g.DrawLine($p, $x + 10, $high, $x + 10, $low); $top = [math]::Min($open, $close); $g.FillRectangle((Brush (Faint $a)), $x, $top, 20, [math]::Max(6, [math]::Abs($close - $open))) }
$line = Pen $W 7; $thin = Pen (Faint 120) 3; $dash = Pen (Faint 170) 4; $dash.DashStyle = 'Dash'
# 0 ignite: flat candles, then a staircase of rising candles eating the sell ladder, arrow to the target line
Cell 0
$g.DrawLine($dash, 40, 52, 470, 52)
foreach ($k in 0..3) { Candle (50 + $k * 34) 196 (190 - ($k % 2) * 6) 182 206 110 }
foreach ($k in 0..4) { $b = 190 - $k * 30; Candle (200 + $k * 38) $b ($b - 34) ($b - 42) ($b + 8) 255 }
$g.DrawLines($line, (Pts @(206,180, 248,150, 286,120, 324,92, 362,64, 400,48)))
$g.FillPolygon((Brush $W), (Pts @(424,38, 392,40, 404,66)))
foreach ($k in 0..5) { $g.FillRectangle((Brush (Faint 90)), 420, 70 + $k * 26, 30 + (5 - $k) * 10, 16) }
# 1 unload: price climbs a staircase; at each step a block of shares leaves to the right
Cell 1
$g.DrawLines($line, (Pts @(40,220, 120,220, 120,180, 200,180, 200,140, 280,140, 280,100, 360,100)))
foreach ($k in 0..3) { $sx = 130 + $k * 80; $sy = 168 - $k * 40; $g.FillRectangle((Brush (Faint 200)), $sx, $sy - 26, 40, 22); $g.DrawLine($thin, $sx + 44, $sy - 15, 440, $sy - 15) }
$g.FillPolygon((Brush $W), (Pts @(470,90, 430,60, 430,120))); $g.DrawRectangle((Pen $W 5), 380, 150, 90, 70)
# 2 slam: heavy arrow driving the price through the buy ladder; broken bars below
Cell 2
$g.FillPolygon((Brush $W), (Pts @(226,20, 286,20, 286,120, 326,120, 256,190, 186,120, 226,120)))
$g.DrawLine((Pen $W 9), 60, 200, 452, 200)
foreach ($k in 0..5) { $len = 70 - $k * 8; $g.FillRectangle((Brush (Faint (200 - $k * 25))), 60 + $k * 66, 212, $len, 14) }
$g.DrawLines((Pen (Faint 160) 4), (Pts @(70,60, 130,70, 160,64)))
$g.DrawLines((Pen (Faint 160) 4), (Pts @(360,64, 400,80, 450,70)))
# 3 close: day timeline with the closing window highlighted and the closing print lifted
Cell 3
$g.DrawLine($thin, 40, 170, 470, 170)
foreach ($k in 0..8) { $g.DrawLine($thin, 40 + $k * 53, 162, 40 + $k * 53, 178) }
$g.FillRectangle((Brush (Faint 60)), 390, 40, 80, 170)
$g.DrawLines($line, (Pts @(40,140, 120,150, 200,132, 280,146, 360,138, 400,130, 430,92, 462,62)))
$g.DrawEllipse((Pen $W 6), 300, 18, 64, 64); $g.DrawLines((Pen $W 5), (Pts @(332,30, 332,50, 346,58)))
$g.FillRectangle((Brush $W), 452, 52, 20, 20)
# 4 wash: buy and sell arrows circling around the same shares, volume bars rising underneath
Cell 4
$g.DrawArc($line, 150, 30, 210, 150, 200, 140); $g.FillPolygon((Brush $W), (Pts @(352,64, 330,34, 316,72)))
$g.DrawArc($line, 150, 30, 210, 150, 20, 140); $g.FillPolygon((Brush $W), (Pts @(158,148, 180,178, 194,140)))
$g.FillRectangle((Brush $W), 236, 84, 40, 40)
foreach ($k in 0..9) { $h = 16 + [math]::Pow($k, 1.45) * 6; $g.FillRectangle((Brush (Faint (90 + $k * 16))), 40 + $k * 44, 246 - $h, 28, $h) }
# 5 wall: price line above; a tall wall of buy orders below it; most bricks hollow (it is for show)
Cell 5
$g.DrawLines($line, (Pts @(40,60, 140,70, 240,58, 340,72, 470,64)))
$g.DrawLine($dash, 40, 120, 470, 120)
foreach ($r in 0..3) { foreach ($c in 0..5) { $bx = 60 + $c * 66 + ($r % 2) * 30; $by = 132 + $r * 28; if ($bx -lt 440) { if (($r + $c) % 3 -eq 0) { $g.FillRectangle((Brush (Faint 230)), $bx, $by, 58, 22) } else { $g.DrawRectangle((Pen (Faint 200) 4), $bx, $by, 58, 22) } } } }
$g.Dispose(); $bmp.Save("$Folder\Emblems.png", [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"baked $Folder"
