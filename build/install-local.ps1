<#
.SYNOPSIS
    Builds the add-in and installs it into this PC's Navisworks Plugins folder
    (<Navisworks>\Plugins\NavisworksIfcExporter). Auto-detects the Navisworks
    install if -NavisworksDir is not given.

.NOTE
    Writing under Program Files needs admin; run from an elevated prompt
    (install-for-me.bat handles elevation for you).
#>
param(
    [string]$Configuration = "Release",
    [string]$NavisworksDir = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Find-NavisDir {
    foreach ($prod in "Manage", "Simulate") {
        foreach ($yr in 2026, 2025, 2024, 2023, 2022) {
            $p = "C:\Program Files\Autodesk\Navisworks $prod $yr"
            if (Test-Path $p) { return $p }
        }
    }
    return $null
}

if (-not $NavisworksDir -or -not (Test-Path $NavisworksDir)) {
    $detected = Find-NavisDir
    if ($detected) {
        $NavisworksDir = $detected
        Write-Host "Detected Navisworks: $NavisworksDir"
    }
    elseif (-not $NavisworksDir) {
        throw "Could not find Navisworks. Pass -NavisworksDir ""C:\Program Files\Autodesk\Navisworks Manage 2024""."
    }
}

# 1. Build + stage (produces artifacts\NavisworksIfcExporter.bundle\Contents).
& (Join-Path $PSScriptRoot "pack-bundle.ps1") -Configuration $Configuration -NavisworksDir $NavisworksDir

# 2. Copy into <Navisworks>\Plugins\NavisworksIfcExporter (folder name MUST match DLL name).
$src  = Join-Path $repoRoot "artifacts\NavisworksIfcExporter.bundle\Contents"
$dest = Join-Path $NavisworksDir "Plugins\NavisworksIfcExporter"

Write-Host "==> Installing to $dest" -ForegroundColor Cyan
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item (Join-Path $src "*") $dest -Recurse -Force

Write-Host ""
Write-Host "Installed. Start Navisworks -> Tool Add-ins -> Export IFC" -ForegroundColor Green
