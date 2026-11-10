<#
.SYNOPSIS
    Draws the setup wizard images: the small tile at the top right of each page and the tall picture on the finish
    page, each in the sizes Inno Setup picks from for 100 to 250 percent display scaling.
.NOTES
    Run once after the tile changes; the images are part of the repository. The tile is the family's (Sunset gradient
    with DynaTones' mark, assets\png from --write-assets), the tall picture is drawn here: the day as DynaTones sees it,
    the sun's arc over pictures from night through the day to night.
#>
param(
    [string]$Logo = (Join-Path $PSScriptRoot '..\assets\png\DynaTones-1024.png'),
    [string]$Out = (Join-Path $PSScriptRoot 'images')
)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null
$logoImage = [Drawing.Image]::FromFile((Resolve-Path $Logo))

function C($hex, $a = 255) { $c = [Drawing.ColorTranslator]::FromHtml($hex); [Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B) }
function Canvas($w, $h) {
    $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.TextRenderingHint = 'AntiAliasGridFit'
    return $bmp, $g
}
function Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2*$r, 2*$r, 180, 90); $p.AddArc($x+$w-2*$r, $y, 2*$r, 2*$r, 270, 90)
    $p.AddArc($x+$w-2*$r, $y+$h-2*$r, 2*$r, 2*$r, 0, 90); $p.AddArc($x, $y+$h-2*$r, 2*$r, 2*$r, 90, 90); $p.CloseFigure(); $p
}
function Gradient($rect, [string[]]$colors, [float]$angle = 0) {
    $b = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, (C $colors[0]), (C $colors[-1]), $angle
    $blend = New-Object Drawing.Drawing2D.ColorBlend $colors.Count
    $blend.Colors = [Drawing.Color[]]($colors | ForEach-Object { C $_ })
    $blend.Positions = [float[]](0..($colors.Count - 1) | ForEach-Object { $_ / ($colors.Count - 1) })
    $b.InterpolationColors = $blend; $b
}

# Small image, top right of each page: the tile on a transparent background (the header is light or dark).
$small = @(@(55, 58), @(64, 68), @(83, 80), @(92, 97), @(110, 106), @(119, 123), @(138, 140))
foreach ($size in $small) {
    $w = $size[0]; $h = $size[1]
    $bmp, $g = Canvas $w $h
    $edge = [Math]::Min($w, $h) * 0.92
    $g.DrawImage($logoImage, [float](($w - $edge) / 2), [float](($h - $edge) / 2), [float]$edge, [float]$edge)
    $g.Dispose(); $bmp.Save((Join-Path $Out "small-$w.png"), [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

# Tall picture of the finish page, drawn at each size (not scaled), so text and lines stay sharp.
$large = @(@(164, 314), @(192, 386), @(246, 459), @(273, 556), @(328, 604), @(355, 700), @(410, 797))
# Pictures of the day, from night through morning, noon and dusk to night: sky and ground.
$day = @(
    @('#0B1026', '#1B2450'), @('#3B2A6B', '#F08A5D'), @('#7FB8F0', '#CFE8FF'),
    @('#5AA0E8', '#BFE0FF'), @('#F59F6B', '#7048E8'), @('#1A1440', '#3A2A6E'))
foreach ($size in $large) {
    $w = $size[0]; $h = $size[1]; $s = $w / 246.0
    $bmp, $g = Canvas $w $h
    $g.Clear((C '#141414'))
    # A warm glow behind the tile, in the gradient's middle tone.
    $glow = New-Object Drawing.Drawing2D.GraphicsPath
    $glow.AddEllipse([float](-0.35 * $w), [float](-0.1 * $h), [float](1.7 * $w), [float](0.55 * $h))
    $pb = New-Object Drawing.Drawing2D.PathGradientBrush $glow
    $pb.CenterColor = C '#FF6B6B' 70; $pb.SurroundColors = [Drawing.Color[]]@((C '#141414' 0))
    $g.FillPath($pb, $glow)

    $tile = 84 * $s; $top = 52 * $s
    $g.DrawImage($logoImage, [float](($w - $tile) / 2), [float]$top, [float]$tile, [float]$tile)

    $center = New-Object Drawing.StringFormat; $center.Alignment = 'Center'
    $title = New-Object Drawing.Font 'Segoe UI Black', ([float](25 * $s)), ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
    $g.DrawString('DynaTones', $title, (New-Object Drawing.SolidBrush (C '#FFFFFF')), (New-Object Drawing.RectangleF 0, ([float]($top + $tile + 14 * $s)), $w, ([float](40 * $s))), $center)
    # No sentence: the picture is the same in every language, the text of the page beside it says what is needed.
    # The day: the sun's arc over the pictures, as on DynaTones' page Background.
    $left = 22 * $s; $width = $w - 2 * $left
    $stripTop = $h - 0.2 * $h; $stripH = 0.085 * $h
    $arcTop = $top + $tile + 78 * $s; $arcBottom = $stripTop - 14 * $s
    $points = foreach ($i in 0..60) {
        $x = $left + $width * $i / 60
        $y = $arcBottom - ($arcBottom - $arcTop) * [Math]::Max([double]0, [Math]::Sin(($i / 60) * [Math]::PI * 1.25 - [Math]::PI * 0.125))
        New-Object Drawing.PointF ([float]$x), ([float]$y)
    }
    $arcRect = New-Object Drawing.RectangleF ([float]$left), ([float]$arcTop), ([float]$width), ([float]($arcBottom - $arcTop + 1))
    $pen = New-Object Drawing.Pen (Gradient $arcRect @('#FFD43B', '#FF6B6B', '#7048E8')), ([float](2.4 * $s))
    $g.DrawLines($pen, [Drawing.PointF[]]$points)
    $base = New-Object Drawing.Pen (C '#FFFFFF' 40), ([float]([Math]::Max(1, $s)))
    $base.DashStyle = 'Dash'
    $g.DrawLine($base, [float]$left, [float]$arcBottom, [float]($left + $width), [float]$arcBottom)
    # The sun now, in the afternoon.
    $sun = $points[38]; $r = 6 * $s
    $g.FillEllipse((New-Object Drawing.SolidBrush (C '#FFD43B')), [float]($sun.X - $r), [float]($sun.Y - $r), [float](2 * $r), [float](2 * $r))
    $g.DrawEllipse((New-Object Drawing.Pen (C '#141414'), ([float](2 * $s))), [float]($sun.X - $r), [float]($sun.Y - $r), [float](2 * $r), [float](2 * $r))

    $gap = 3 * $s; $cell = ($width - $gap * ($day.Count - 1)) / $day.Count
    for ($i = 0; $i -lt $day.Count; $i++) {
        $x = $left + $i * ($cell + $gap)
        $rect = New-Object Drawing.RectangleF ([float]$x), ([float]$stripTop), ([float]$cell), ([float]$stripH)
        $g.FillPath((Gradient $rect $day[$i] 90), (Rounded $x $stripTop $cell $stripH (4 * $s)))
        # A low hill as ground, the same in every picture.
        $hill = New-Object Drawing.Drawing2D.GraphicsPath
        $hill.AddEllipse([float]($x - 0.2 * $cell), [float]($stripTop + 0.62 * $stripH), [float](1.4 * $cell), [float]($stripH))
        $g.SetClip((Rounded $x $stripTop $cell $stripH (4 * $s)))
        $g.FillPath((New-Object Drawing.SolidBrush (C '#000000' 90)), $hill)
        $g.ResetClip()
    }

    # The family's brand line at the bottom edge.
    $bar = New-Object Drawing.RectangleF 0, ([float]($h - 4 * $s)), $w, ([float](4 * $s))
    $g.FillRectangle((Gradient $bar @('#FFD43B', '#FF6B6B', '#7048E8')), $bar)
    $g.Dispose(); $bmp.Save((Join-Path $Out "large-$w.png"), [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

$logoImage.Dispose()
Get-ChildItem $Out | ForEach-Object { $_.Name }
