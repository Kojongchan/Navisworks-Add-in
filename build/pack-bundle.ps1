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
    [string]$NavisworksDir = "C:\Program Files\Autodesk\Navisworks Manage 2022",
    # Optional code signing of the add-in DLL.
    [string]$SignPfx = "",
    [string]$SignPassword = "",
    [string]$SignThumbprint = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
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

# Find a build tool: prefer MSBuild (from VS / Build Tools), fall back to dotnet.
function Resolve-BuildTool {
    $msb = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($msb) { return @{ Exe = $msb.Source; Kind = "msbuild" } }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
            -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
        if ($path) { return @{ Exe = $path; Kind = "msbuild" } }
    }

    $dn = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dn) { return @{ Exe = $dn.Source; Kind = "dotnet" } }

    throw "No build tool found. Install Visual Studio 2022 (or Build Tools for Visual Studio) or the .NET SDK."
}

$tool = Resolve-BuildTool
Write-Host "    using $($tool.Kind): $($tool.Exe)"
if ($tool.Kind -eq "msbuild") {
    & $tool.Exe $project /restore /p:Configuration=$Configuration /p:Platform=x64 /p:NavisworksDir=$NavisworksDir
} else {
    & $tool.Exe build $project -c $Configuration -p:Platform=x64 -p:NavisworksDir=$NavisworksDir
}
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

Write-Host "==> Staging bundle" -ForegroundColor Cyan
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stageInner -Force | Out-Null
Copy-Item (Join-Path $bundleSrc "PackageContents.xml") $stage

# Ship static resources (icons) but not the developer README placeholder.
$resourcesSrc = Join-Path $bundleSrc "Contents\Resources"
if (Test-Path $resourcesSrc) {
    Copy-Item $resourcesSrc (Join-Path $stageInner "Resources") -Recurse
}

Get-ChildItem $buildOut -File |
    Where-Object { $_.Extension -in ".dll", ".pdb", ".config" -and $excludeDlls -notcontains $_.Name } |
    ForEach-Object { Copy-Item $_.FullName $stageInner }

$addinDll = Join-Path $stageInner "NavisworksIfcExporter.dll"
if (-not (Test-Path $addinDll)) {
    throw "NavisworksIfcExporter.dll not found in build output. Did the build succeed?"
}

if ($SignPfx -or $SignThumbprint) {
    Write-Host "==> Signing add-in DLL" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "sign.ps1") -Files $addinDll `
        -PfxPath $SignPfx -PfxPassword $SignPassword -Thumbprint $SignThumbprint -TimestampUrl $TimestampUrl
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
