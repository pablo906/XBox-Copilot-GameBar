<#
.SYNOPSIS
  Creates the self-signed certificate that GitHub Actions uses to sign release packages.

.DESCRIPTION
  Run this once on your own PC. It creates a code-signing certificate, exports it to a
  password-protected .pfx outside the repo, and copies the base64 text for the
  SIGNING_CERT_PFX_BASE64 repository secret to your clipboard.

  Keep the .pfx and password private and never commit them. Reuse the same certificate
  for every release so users only trust it once.

.EXAMPLE
  .\scripts\New-SigningCertificate.ps1 -Subject "CN=Jose Adams"
#>
param(
  [string]$Subject = 'CN=CopilotGameBarBridge',
  [string]$OutFile = (Join-Path $HOME 'CopilotGameBarBridge-signing.pfx'),
  [int]$Years = 5
)

$ErrorActionPreference = 'Stop'

$password = Read-Host -AsSecureString 'Choose a password for the .pfx (save it as SIGNING_CERT_PASSWORD)'

$cert = New-SelfSignedCertificate -Type Custom -Subject $Subject `
  -KeyUsage DigitalSignature -FriendlyName 'Copilot Game Bar Bridge signing' `
  -CertStoreLocation 'Cert:\CurrentUser\My' `
  -NotAfter (Get-Date).AddYears($Years) `
  -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

Export-PfxCertificate -Cert $cert -FilePath $OutFile -Password $password | Out-Null

# The exported .pfx is all CI needs, so drop the copy from the local certificate store.
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)"

[Convert]::ToBase64String([IO.File]::ReadAllBytes($OutFile)) | Set-Clipboard

Write-Host ""
Write-Host "Certificate subject: $Subject"
Write-Host "Saved to:            $OutFile"
Write-Host ""
Write-Host "The base64 text for SIGNING_CERT_PFX_BASE64 is now on your clipboard."
Write-Host "Add it, and the password you chose as SIGNING_CERT_PASSWORD, under"
Write-Host "GitHub repo > Settings > Secrets and variables > Actions > New repository secret."
