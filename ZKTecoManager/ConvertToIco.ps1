Add-Type -AssemblyName System.Drawing

$pngPath = Join-Path $PSScriptRoot "..\app logo.png"
$icoPath = Join-Path $PSScriptRoot "app_logo.ico"

$img = [System.Drawing.Image]::FromFile($pngPath)
$bitmap = New-Object System.Drawing.Bitmap $img, 256, 256
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())

$fs = [System.IO.File]::Create($icoPath)
$icon.Save($fs)
$fs.Close()

$icon.Dispose()
$bitmap.Dispose()
$img.Dispose()

Write-Host "ICO created: $icoPath"
