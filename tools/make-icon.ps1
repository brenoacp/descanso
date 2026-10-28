# Gera Assets\descanso.ico com frames DIB (BITMAPINFOHEADER + BGRA + máscara AND)
# e uma prévia em docs\icon.png para o README.
# DIB é o formato que GDI+, a bandeja e o shell leem sem depender de suporte a PNG.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root 'src\Descanso.App\Assets\descanso.ico'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Get-DibPayload([System.Drawing.Bitmap]$bitmap) {
    $size = $bitmap.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $locked = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $locked.Stride
    $pixels = New-Object byte[] ($stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
    $bitmap.UnlockBits($locked)

    # Máscara AND: obrigatória no formato, toda zero porque a transparência vem do alfa.
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $maskBytes = $maskStride * $size

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    $bw.Write([UInt32]40)                    # biSize
    $bw.Write([Int32]$size)                  # biWidth
    $bw.Write([Int32]($size * 2))            # biHeight: XOR + AND
    $bw.Write([UInt16]1)                     # biPlanes
    $bw.Write([UInt16]32)                    # biBitCount
    $bw.Write([UInt32]0)                     # biCompression: BI_RGB
    $bw.Write([UInt32]($size * $size * 4))   # biSizeImage
    $bw.Write([Int32]0)                      # biXPelsPerMeter
    $bw.Write([Int32]0)                      # biYPelsPerMeter
    $bw.Write([UInt32]0)                     # biClrUsed
    $bw.Write([UInt32]0)                     # biClrImportant

    # Bitmap DIB é guardado de baixo para cima.
    for ($y = $size - 1; $y -ge 0; $y--) {
        $start = $y * $stride
        $bw.Write([byte[]]$pixels[$start..($start + $size * 4 - 1)])
    }

    $bw.Write((New-Object byte[] $maskBytes))
    $bw.Flush()

    $payload = $ms.ToArray()
    $bw.Dispose()
    $ms.Dispose()
    return , $payload
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $radius = [single]([Math]::Max(2, $size * 0.22))
    $shape = New-RoundedPath 0 0 $size $size $radius

    $start = New-Object System.Drawing.Point(0, 0)
    $end = New-Object System.Drawing.Point($size, $size)

    # Verde-água para azul-petróleo: calmo, sem competir com os ícones do sistema.
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $start, $end,
        [System.Drawing.Color]::FromArgb(255, 26, 187, 168),
        [System.Drawing.Color]::FromArgb(255, 10, 82, 104))
    $g.FillPath($gradient, $shape)

    # Brilho superior. O degradê cobre a altura toda com uma parada em 55% para
    # não repetir o padrão (LinearGradientBrush não aceita WrapMode.Clamp).
    $glowEnd = New-Object System.Drawing.Point(0, $size)
    $glow = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $start, $glowEnd,
        [System.Drawing.Color]::FromArgb(46, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend(3)
    $blend.Colors = @(
        [System.Drawing.Color]::FromArgb(46, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $blend.Positions = @(0.0, 0.55, 1.0)
    $glow.InterpolationColors = $blend
    $g.FillPath($glow, $shape)

    # Duas barras de pausa: o símbolo mais legível em 16 px.
    $barW = [single]($size * 0.15)
    $barH = [single]($size * 0.42)
    $gap = [single]($size * 0.13)
    $left = [single](($size - ($barW * 2 + $gap)) / 2)
    $top = [single](($size - $barH) / 2)
    $barRadius = [single]($barW / 2)

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(242, 255, 255, 255))
    $g.FillPath($white, (New-RoundedPath $left $top $barW $barH $barRadius))
    $g.FillPath($white, (New-RoundedPath ($left + $barW + $gap) $top $barW $barH $barRadius))

    $g.Dispose()
    $shape.Dispose()
    $gradient.Dispose()
    $glow.Dispose()
    $white.Dispose()

    return $bmp
}

function New-IconFrame([int]$size) {
    $bmp = New-IconBitmap $size
    $payload = Get-DibPayload $bmp
    $bmp.Dispose()

    return , $payload
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($s in $sizes) {
    $frames += , @{ Size = $s; Bytes = (New-IconFrame $s) }
}

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)

$writer.Write([UInt16]0)                 # reservado
$writer.Write([UInt16]1)                 # tipo: ícone
$writer.Write([UInt16]$frames.Count)

$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $dim = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)               # cores da paleta
    $writer.Write([byte]0)               # reservado
    $writer.Write([UInt16]1)             # planos
    $writer.Write([UInt16]32)            # bits por pixel
    $writer.Write([UInt32]$frame.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $frame.Bytes.Length
}

foreach ($frame in $frames) {
    $writer.Write([byte[]]$frame.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($dest, $stream.ToArray())
$writer.Dispose()
$stream.Dispose()

# Prévia em PNG para o README (o GitHub não renderiza .ico).
$previewDir = Join-Path $root 'docs'
New-Item -ItemType Directory -Force -Path $previewDir | Out-Null
$preview = New-IconBitmap 256
$preview.Save((Join-Path $previewDir 'icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

Write-Host "Ícone gerado: $dest"
