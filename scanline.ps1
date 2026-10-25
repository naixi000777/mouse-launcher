$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(1120, 640)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(720, 376, 0, 0, (New-Object System.Drawing.Size(1120, 640)))
$g.Dispose()
foreach ($y in 88..112) {
    $row = @()
    foreach ($x in 195..215) { $row += $bmp.GetPixel($x, $y).R }
    Write-Output ("y=$y  R: " + ($row -join ","))
}
$bmp.Dispose()

