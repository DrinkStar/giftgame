# SeaAnomaly release build script — Debug-agnostic one-click Release export.
# Usage:  ./build-release.ps1            (from the game/ directory)
# Output: build/SeaAnomaly.exe + build/SeaAnomaly.pck (Windows Desktop)

$ErrorActionPreference = "Stop"
$GODOT = if ($env:GODOT) { $env:GODOT } else { "godot" }

Write-Host "==> dotnet build -c Release"
# Build the .csproj explicitly: the .sln has no Release configuration.
dotnet build SeaAnomaly.csproj -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw "Release build failed" }

Write-Host "==> Import (refresh sidecars for any new assets)"
& $GODOT --headless --path . --import
if ($LASTEXITCODE -ne 0) { throw "Import failed" }

Write-Host "==> Export Windows Desktop"
if (-not (Test-Path "build")) { New-Item -ItemType Directory -Path "build" | Out-Null }
& $GODOT --headless --path . --export-release "Windows Desktop" build/SeaAnomaly.exe
if ($LASTEXITCODE -ne 0) { throw "Export failed" }

Write-Host "==> Verify artifacts"
$exe = Get-Item "build/SeaAnomaly.exe" -ErrorAction Stop
$pck = Get-Item "build/SeaAnomaly.pck" -ErrorAction Stop
Write-Host "OK  $($exe.Name)  $([Math]::Round($exe.Length/1MB,1)) MB"
Write-Host "OK  $($pck.Name)  $([Math]::Round($pck.Length/1MB,1)) MB"

Write-Host "==> Full test suite"
& $GODOT --headless --path . --run-tests --quit-on-finish
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
Write-Host "Done. Ship it."
