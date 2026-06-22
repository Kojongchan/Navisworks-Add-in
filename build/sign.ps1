<#
.SYNOPSIS
    Authenticode-signs one or more files with signtool (SHA-256 + RFC3161
    timestamp). Accepts either a PFX file or a certificate thumbprint.

.EXAMPLE
    pwsh build/sign.ps1 -Files "a.dll","Setup.exe" -PfxPath installer\CHAN-codesign.pfx -PfxPassword pw
    pwsh build/sign.ps1 -Files "Setup.exe" -Thumbprint 1234ABCD...
#>
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$Thumbprint = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$SignTool = ""
)

$ErrorActionPreference = "Stop"

if (-not $PfxPath -and -not $Thumbprint) {
    throw "Provide either -PfxPath (with -PfxPassword) or -Thumbprint."
}

# Locate signtool.exe (ships with the Windows 10/11 SDK).
if (-not $SignTool) {
    $found = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "x64" } |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($found) { $SignTool = $found.FullName }
}
if (-not $SignTool -or -not (Test-Path $SignTool)) {
    throw "signtool.exe not found. Install the Windows SDK or pass -SignTool <path>."
}

$existing = $Files | Where-Object { Test-Path $_ }
if (-not $existing) { throw "None of the files to sign exist." }

foreach ($file in $existing) {
    Write-Host "==> Signing $file" -ForegroundColor Cyan
    if ($PfxPath) {
        & $SignTool sign /fd SHA256 /tr $TimestampUrl /td SHA256 /f $PfxPath /p $PfxPassword $file
    }
    else {
        & $SignTool sign /fd SHA256 /tr $TimestampUrl /td SHA256 /sha1 $Thumbprint $file
    }
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $file (exit $LASTEXITCODE)." }
}

Write-Host "Signed $($existing.Count) file(s)." -ForegroundColor Green
