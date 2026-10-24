@echo off
rem 构建安装包: 编译主程序(内嵌鼠鼠图片) -> 打包 payload.zip -> 编译内嵌资源的安装器
cd /d D:\老鼠启动器
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REF=/r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Newtonsoft.Json.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll

%CSC% /nologo /target:winexe /platform:anycpu /codepage:65001 /win32icon:app.ico /resource:icon-preview.png,icon-preview.png /out:老鼠启动器.exe %REF% MouseLauncher.cs || goto :fail
%CSC% /nologo /target:exe /platform:anycpu /codepage:65001 /win32icon:app.ico /define:CLI /out:mlc.exe %REF% MouseLauncher.cs || goto :fail

powershell -NoProfile -Command "Remove-Item installer\payload.zip -Force -ErrorAction SilentlyContinue; $files = @('老鼠启动器.exe','mlc.exe','Newtonsoft.Json.dll','MouseLauncher.cs','build.bat','app.ico','icon-preview.png'); Add-Type -AssemblyName System.IO.Compression.FileSystem; $zip = [System.IO.Compression.ZipFile]::Open('D:\老鼠启动器\installer\payload.zip','Create'); foreach ($f in $files) { [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, 'D:\老鼠启动器\' + $f, $f) | Out-Null }; $zip.Dispose()" || goto :fail
if not exist installer\payload.zip goto :fail

cd installer
%CSC% /nologo /target:winexe /platform:anycpu /codepage:65001 /win32icon:..\app.ico /resource:payload.zip,payload.zip /resource:..\icon-preview.png,icon-preview.png /out:"..\老鼠启动器-安装包.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll InstallerStub.cs || goto :fail
cd ..

echo.
echo BUILD OK: 老鼠启动器-安装包.exe
pause
exit /b 0
:fail
echo BUILD FAILED
pause
exit /b 1
