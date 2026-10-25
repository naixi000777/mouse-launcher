$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct RECT { public int L, T, R, B; }
public class W3 {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@
$p = Get-Process | Where-Object { try { $_.Path -eq 'D:\老鼠启动器\老鼠启动器.exe' } catch { $false } } | Select-Object -First 1
$h = $p.MainWindowHandle
if ($h -ne [IntPtr]::Zero) { [W3]::SetForegroundWindow($h) | Out-Null }
Start-Sleep -Milliseconds 500
$r = New-Object RECT
[W3]::GetWindowRect($h, [ref]$r) | Out-Null
$bmp = New-Object System.Drawing.Bitmap(($r.R - $r.L), ($r.B - $r.T))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size(($r.R - $r.L), ($r.B - $r.T))))
$g.Dispose()
function Probe([string]$name, [int]$x, [int]$y, [int]$pw, [int]$ph) {
    $card = 0; $ctrl = 0; $bg = 0; $bright = 0; $green = 0; $total = 0
    for ($i = $x; $i -lt $x + $pw; $i += 2) {
        for ($j = $y; $j -lt $y + $ph; $j += 2) {
            $c = $bmp.GetPixel($i, $j)
            $total++
            if ([Math]::Abs($c.R - 50) -le 6 -and [Math]::Abs($c.G - 50) -le 6 -and [Math]::Abs($c.B - 60) -le 6) { $card++ }
            elseif ([Math]::Abs($c.R - 62) -le 6 -and [Math]::Abs($c.G - 62) -le 6 -and [Math]::Abs($c.B - 74) -le 6) { $ctrl++ }
            elseif ([Math]::Abs($c.R - 38) -le 5 -and [Math]::Abs($c.G - 38) -le 5 -and [Math]::Abs($c.B - 46) -le 5) { $bg++ }
            if ($c.R -gt 150 -and $c.G -gt 150 -and $c.B -gt 150) { $bright++ }
            if ($c.G -gt 130 -and $c.R -lt 130 -and $c.G -gt $c.R + 40) { $green++ }
        }
    }
    Write-Output ("{0}: total={1} card={2} ctrl={3} bg={4} bright={5} green={6}" -f $name, $total, $card, $ctrl, $bg, $bright, $green)
}
Probe 'JavaCombo区' 640 88 336 22
Probe 'Java提示(绿)' 900 66 130 18
Probe '版本框区' 206 196 256 22
Probe '状态标签区' 900 296 200 16
$bmp.Dispose()
