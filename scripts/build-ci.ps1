<#
  build-ci.ps1 — CI / GitHub Actions 构建脚本 (UTF-8 with BOM)

  替代 GBK 编码的 build.bat：后者在英文区域设置的 Windows runner 上
  中文路径会乱码，且末尾的 pause 会挂住 CI 任务。
  本脚本供 GitHub-hosted Windows runner 使用（SignPath OSS 项目的硬性要求）。

  用法：
    powershell -ExecutionPolicy Bypass -File scripts\build-ci.ps1
    pwsh -File scripts/build-ci.ps1 -Stage repack

  Stage:
    build  = 完整构建：主程序 -> CLI -> payload.zip -> 安装包（默认）
    repack = 主程序/CLI 已经是签名版时，只重建 payload.zip 并重编译安装包
             （用于 SignPath 两阶段签名流程的第二阶段）
#>
[CmdletBinding()]
param(
    [ValidateSet('build', 'repack')]
    [string]$Stage = 'build',
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputDir) { $OutputDir = Join-Path $root 'dist' }
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$mainExe  = Join-Path $OutputDir '老鼠启动器.exe'
$cliExe   = Join-Path $OutputDir 'mlc.exe'
$setupExe = Join-Path $OutputDir '老鼠启动器-安装包.exe'
$payload  = Join-Path $OutputDir 'payload.zip'

# 优先用系统自带的 .NET Framework 编译器（C# 5，与本地 build.bat 一致）
$cscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $csc) {
    $roslyn = Get-ChildItem -Path 'C:\Program Files\Microsoft Visual Studio' `
        -Filter 'csc.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'Roslyn' } | Select-Object -First 1
    if ($roslyn) { $csc = $roslyn.FullName }
}
if (-not $csc) { throw '找不到 csc.exe（需要 .NET Framework 4.x 编译器）' }
Write-Host "csc: $csc"

$jsonDll = Join-Path $root 'Newtonsoft.Json.dll'
$source  = Join-Path $root 'MouseLauncher.cs'
$icon    = Join-Path $root 'app.ico'
$png     = Join-Path $root 'icon-preview.png'
$stub    = Join-Path $root 'installer\InstallerStub.cs'
$bat     = Join-Path $root 'build.bat'

foreach ($p in @($jsonDll, $source, $icon, $png, $stub)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "缺少构建输入: $p" }
}

$common = @('/nologo', '/platform:anycpu', '/codepage:65001')
$mainRefs = @(
    '/r:System.Windows.Forms.dll',
    '/r:System.Drawing.dll',
    "/r:$jsonDll",
    '/r:System.IO.Compression.dll',
    '/r:System.IO.Compression.FileSystem.dll'
)
$stubRefs = @(
    '/r:System.Windows.Forms.dll',
    '/r:System.Drawing.dll',
    '/r:System.IO.Compression.dll',
    '/r:System.IO.Compression.FileSystem.dll'
)

function Invoke-Csc([string[]]$arguments) {
    & $csc @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "csc 失败 (exit $LASTEXITCODE): $($arguments -join ' ')"
    }
}

if ($Stage -eq 'build') {
    Write-Host '[1/4] 编译主程序'
    $a = $common + @(
        '/target:winexe',
        "/win32icon:$icon",
        "/resource:${png},icon-preview.png",
        "/out:$mainExe"
    ) + $mainRefs + @($source)
    Invoke-Csc $a

    Write-Host '[2/4] 编译 CLI'
    $a = $common + @(
        '/target:exe',
        '/define:CLI',
        "/win32icon:$icon",
        "/out:$cliExe"
    ) + $mainRefs + @($source)
    Invoke-Csc $a
}
else {
    Write-Host '[repack] 主程序/CLI 视为已签名版本，跳过编译'
    foreach ($p in @($mainExe, $cliExe)) {
        if (-not (Test-Path -LiteralPath $p)) { throw "缺少已签名文件: $p" }
    }
}

Write-Host '[3/4] 重建 payload.zip'
# 用 Compress-Archive 而不是直接调 ZipFile/ZipArchiveMode：
# Windows PowerShell 5.1 与 pwsh 7 的程序集加载行为不同，直接引用类型容易踩空。
# 先集中复制到一个暂存目录，保证 zip 里的 entry 名就是纯文件名
# （InstallerStub.cs 用 e.Name 落地文件，entry 名必须含中文主程序名）。
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload -Force }
$packDir = Join-Path $OutputDir '_pack'
if (Test-Path -LiteralPath $packDir) { Remove-Item -LiteralPath $packDir -Recurse -Force }
New-Item -ItemType Directory -Path $packDir -Force | Out-Null
try {
    $pack = [ordered]@{
        '老鼠启动器.exe'      = $mainExe
        'mlc.exe'             = $cliExe
        'Newtonsoft.Json.dll' = $jsonDll
        'MouseLauncher.cs'    = $source
        'build.bat'           = $bat
        'app.ico'             = $icon
        'icon-preview.png'    = $png
    }
    foreach ($k in $pack.Keys) {
        Copy-Item -LiteralPath $pack[$k] -Destination (Join-Path $packDir $k) -Force
    }
    Compress-Archive -Path (Join-Path $packDir '*') -DestinationPath $payload `
        -CompressionLevel Optimal -Force
}
finally {
    Remove-Item -LiteralPath $packDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host '[4/4] 编译安装包（把 payload.zip 作为嵌入资源）'
$a = $common + @(
    '/target:winexe',
    "/win32icon:$icon",
    "/resource:${payload},payload.zip",
    "/resource:${png},icon-preview.png",
    "/out:$setupExe"
) + $stubRefs + @($stub)
Invoke-Csc $a

Write-Host '--- 产物 ---'
foreach ($p in @($mainExe, $cliExe, $setupExe, $payload)) {
    $i = Get-Item -LiteralPath $p
    $h = (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash
    Write-Host ("{0,-26} {1,10} bytes  {2}" -f $i.Name, $i.Length, $h)
}
Write-Host "BUILD OK ($Stage) -> $OutputDir"
