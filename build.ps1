# Compila o TurboFPS usando o compilador C# que já vem com o Windows (.NET Framework 4.x).
# Uso:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe não encontrado. Instale o .NET Framework 4.8.' }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$exe = Join-Path $dist 'TurboFPS.exe'

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    "/out:$exe" `
    "/win32manifest:$(Join-Path $root 'app.manifest')" `
    "/win32icon:$(Join-Path $root 'app.ico')" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll `
    "$(Join-Path $root 'src\*.cs')"
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }
Write-Host "Pronto: $exe"
