# Script tao file .ico tu PNG va nhung vao exe
# Su dung System.Drawing de tao multi-size ICO

Add-Type -AssemblyName System.Drawing

$pngPath = "C:\Users\DiepPC\.gemini\antigravity\scratch\DriverChecker\logo.png"
$icoPath = "C:\Users\DiepPC\.gemini\antigravity\scratch\DriverChecker\app.ico"

if (-not (Test-Path $pngPath)) {
    Write-Host "LOI: Chua co file logo.png"
    exit 1
}

# Load anh goc
$srcImg = [System.Drawing.Image]::FromFile($pngPath)

# Tao ICO voi nhieu kich thuoc
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$ms = New-Object System.IO.MemoryStream

# ICO header
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([UInt16]0)       # Reserved
$writer.Write([UInt16]1)       # Type (1 = ICO)
$writer.Write([UInt16]$sizes.Count)  # Num images

# Tao tung kich thuoc anh
$imageData = @()
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($srcImg, 0, 0, $size, $size)
    $g.Dispose()

    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageData += ,($pngMs.ToArray())
    $pngMs.Dispose()
    $bmp.Dispose()
}

# Tinh offset bat dau cua image data
# Header = 6 bytes, moi entry = 16 bytes
$dataOffset = 6 + ($sizes.Count * 16)

# Viet ICO directory entries
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $data = $imageData[$i]
    
    $w = if ($size -ge 256) { 0 } else { $size }
    $h = if ($size -ge 256) { 0 } else { $size }
    
    $writer.Write([byte]$w)           # Width
    $writer.Write([byte]$h)           # Height
    $writer.Write([byte]0)            # Color palette
    $writer.Write([byte]0)            # Reserved
    $writer.Write([UInt16]1)          # Color planes
    $writer.Write([UInt16]32)         # Bits per pixel
    $writer.Write([UInt32]$data.Length) # Size of image data
    $writer.Write([UInt32]$dataOffset) # Offset to image data
    
    $dataOffset += $data.Length
}

# Viet image data
foreach ($data in $imageData) {
    $writer.Write($data)
}

# Luu file .ico
$writer.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())

$writer.Dispose()
$ms.Dispose()
$srcImg.Dispose()

$icoFile = Get-Item $icoPath
Write-Host "Tao ICO thanh cong: $($icoFile.FullName) ($([math]::Round($icoFile.Length/1024, 1)) KB)"
Write-Host "Bao gom $($sizes.Count) kich thuoc: $($sizes -join ', ')"
