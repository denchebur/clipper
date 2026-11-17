# Сборка установщика Windows на своём компьютере.
# Нужны .NET 8 SDK и Inno Setup 6 (winget install JRSoftware.InnoSetup).
# Запуск из корня репозитория:  powershell -ExecutionPolicy Bypass -File build\windows\build.ps1 1.0.0
param([string]$Version = "0.0.0-dev")
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\.."

dotnet publish "$root\src\Clipper.Desktop" -c Release -r win-x64 --self-contained -p:Version=$Version -o "$root\publish\win-x64"

$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Не найден Inno Setup 6. Установите: winget install JRSoftware.InnoSetup" }

$env:CLIPPER_VERSION = $Version
& $iscc "$PSScriptRoot\clipper.iss"
Write-Host "Готово: $PSScriptRoot\Output\ClipperSetup-$Version-win-x64.exe"
