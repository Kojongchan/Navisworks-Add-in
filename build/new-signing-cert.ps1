<#
.SYNOPSIS
    Creates a self-signed code-signing certificate for "CHAN" and exports a PFX
    you can use with build/sign.ps1.

.DESCRIPTION
    Run this ONCE to get installer/CHAN-codesign.pfx. Keep the PFX private
    (it is git-ignored).

    IMPORTANT — what self-signing does and doesn't do:
      * It lets you sign your DLLs/installer so the signature is consistent and
        tamper-evident.
      * It does NOT make Windows trust you on other people's machines. Their
        SmartScreen / "Unknown publisher" prompt remains unless the public
        certificate (CHAN-codesign.cer) is installed into their Trusted
        Publishers / Trusted Root store (e.g. pushed via Group Policy on a
        managed network).
      * For public distribution (Autodesk App Store, internet downloads), buy an
        OV or EV code-signing certificate from a CA. build/sign.ps1 works with
        that PFX too — just point -PfxPath at it.

.EXAMPLE
    pwsh build/new-signing-cert.ps1 -Password "your-strong-password"
#>
param(
    [string]$Subject = "CN=CHAN, O=CHAN",
    [string]$OutPfx = "",
    [string]$Password = "changeit",
    [int]$ValidYears = 5
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutPfx) { $OutPfx = Join-Path $repoRoot "installer\CHAN-codesign.pfx" }
$outCer = [System.IO.Path]::ChangeExtension($OutPfx, ".cer")

Write-Host "==> Creating self-signed code-signing certificate ($Subject)" -ForegroundColor Cyan
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyUsage DigitalSignature `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears($ValidYears) `
    -HashAlgorithm SHA256

$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $OutPfx -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath $outCer | Out-Null

Write-Host ""
Write-Host "PFX (keep private): $OutPfx"      -ForegroundColor Green
Write-Host "Public cert:        $outCer"       -ForegroundColor Green
Write-Host "Thumbprint:         $($cert.Thumbprint)"
Write-Host ""
Write-Host "Sign with:  pwsh build/build-installer.ps1 -SignPfx `"$OutPfx`" -SignPassword `"<password>`""
