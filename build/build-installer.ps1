<#
.SYNOPSIS
    Builds the add-in, stages the Autodesk bundle, then compiles the Inno Setup
    installer into a single double-click setup.exe.

.DESCRIPTION
    Wraps build/pack-bundle.ps1 (which builds + stages artifacts/<bundle>) and
    then runs Inno Setup's ISCC.exe on installer/NavisworksIfcExporter.iss.

    Inno Setup 6 must be installed: https://jrsoftware.org/isdl.php

.EXAMPLE
    pwsh build/build-installer.ps1
    pwsh build/build-installer.ps1 -Configuration Release -Iscc "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

.OUTPUTS
    artifacts/NavisworksIfcExporter-Setup-<version>.exe
#>
param(
    [string]$Configuration = "Release",
    [string]$NavisworksDir = "C:\Program Files\Autodesk\Navisworks Manage 2022",
    [string]$Iscc = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$iss      = Join-Path $repoRoot "installer\NavisworksIfcExporter.iss"

# 1. Build + stage the bundle (produces artifacts/NavisworksIfcExporter.bundle).
Write-Host "==> Building and staging bundle" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "pack-bundle.ps1") -Configuration $Configuration -NavisworksDir $NavisworksDir

# 2. Locate ISCC.exe.
if (-not $Iscc) {
    $candidates = @(
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    )
    $Iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc -or -not (Test-Path $Iscc)) {
    throw "ISCC.exe not found. Install Inno Setup 6 (https://jrsoftware.org/isdl.php) or pass -Iscc <path>."
}

# 3. Compile the installer.
Write-Host "==> Compiling installer with $Iscc" -ForegroundColor Cyan
& $Iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed (exit $LASTEXITCODE)." }

Write-Host ""
Write-Host "Installer written to: $(Join-Path $repoRoot 'artifacts')" -ForegroundColor Green
