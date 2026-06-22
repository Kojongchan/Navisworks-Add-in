<#
.SYNOPSIS
    Builds the add-in and assembles the Autodesk Application Bundle, then zips it
    for distribution (Autodesk App Store submission or manual install).

.EXAMPLE
    pwsh build/pack-bundle.ps1
    pwsh build/pack-bundle.ps1 -Configuration Release -NavisworksDir "C:\Program Files\Autodesk\Navisworks Manage 2022"

.OUTPUTS
    artifacts/NavisworksIfcExporter.bundle/      (the installable bundle folder)
    artifacts/NavisworksIfcExporter-bundle.zip   (zipped for distribution)
#>
param(
    [string]$Configuration = "Release",
    [string]$NavisworksDir = "C:\Program Files\Autodesk\Navisworks Manage 2022"
)

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $repoRoot "src\NavisworksIfcExporter\NavisworksIfcExporter.csproj"
$bundleSrc  = Join-Path $repoRoot "deploy\NavisworksIfcExporter.bundle"
$buildOut   = Join-Path $repoRoot "src\NavisworksIfcExporter\bin\x64\$Configuration"
$artifacts  = Join-Path $repoRoot "artifacts"
$stage      = Join-Path $artifacts "NavisworksIfcExporter.bundle"
$stageInner = Join-Path $stage "Contents"

# Navisworks API DLLs ship with the product; never redistribute them.
$excludeDlls = @(
    "Autodesk.Navisworks.Api.dll",
    "Autodesk.Navisworks.ComApi.dll",
    "Autodesk.Navisworks.Interop.ComApi.dll"
)

Write-Host "==> Building $Configuration (x64)" -ForegroundColor Cyan
& msbuild $project /restore /p:Configuration=$Configuration /p:Platform=x64 /p:NavisworksDir=$NavisworksDir
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

Write-Host "==> Staging bundle" -ForegroundColor Cyan
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stageInner -Force | Out-Null
Copy-Item (Join-Path $bundleSrc "PackageContents.xml") $stage

Get-ChildItem $buildOut -File |
    Where-Object { $_.Extension -in ".dll", ".pdb", ".config" -and $excludeDlls -notcontains $_.Name } |
    ForEach-Object { Copy-Item $_.FullName $stageInner }

if (-not (Test-Path (Join-Path $stageInner "NavisworksIfcExporter.dll"))) {
    throw "NavisworksIfcExporter.dll not found in build output. Did the build succeed?"
}

Write-Host "==> Zipping" -ForegroundColor Cyan
$zip = Join-Path $artifacts "NavisworksIfcExporter-bundle.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip

Write-Host ""
Write-Host "Bundle folder: $stage" -ForegroundColor Green
Write-Host "Distributable: $zip"   -ForegroundColor Green
Write-Host ""
Write-Host "To install locally, copy the bundle folder into:"
Write-Host "  %APPDATA%\Autodesk\ApplicationPlugins\"
