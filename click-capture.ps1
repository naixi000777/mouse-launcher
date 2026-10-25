$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class M2 {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@
[M2]::SetCursorPos(696 + 330, 421 + 111)
Start-Sleep -Milliseconds 150
[M2]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[M2]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 350
$bmp = New-Object System.Drawing.Bitmap(1120, 640)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(696, 421, 0, 0, (New-Object System.Drawing.Size(1120, 640)))
$g.Dispose()
$bmp.Save('D:\popup-check2.png', [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output SAVED
