<#
.SYNOPSIS
  Installs (or updates) Custom Dock Text from the latest GitHub release.

.DESCRIPTION
  Run from PowerShell:
    irm https://raw.githubusercontent.com/Arihant25/custom-dock-text/main/install.ps1 | iex

  1. Downloads the .msix for your CPU and the signing certificate.
  2. Trusts the certificate for app installs (one UAC prompt, first install only).
     The package is self-signed, so Windows needs this before it will install it.
  3. Installs the package.
#>
$ErrorActionPreference = 'Stop'
$repo = 'Arihant25/custom-dock-text'
$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }

Write-Host "Custom Dock Text installer ($arch)" -ForegroundColor Cyan
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers @{ 'User-Agent' = 'custom-dock-text-installer' }
$msixAsset = $release.assets | Where-Object { $_.name -like "*_$arch.msix" } | Select-Object -First 1
$cerAsset = $release.assets | Where-Object { $_.name -like '*.cer' } | Select-Object -First 1
if (-not $msixAsset -or -not $cerAsset) { throw "Release $($release.tag_name) has no $arch package." }

$dir = Join-Path $env:TEMP "custom-dock-text-$($release.tag_name)"
New-Item -ItemType Directory -Force $dir | Out-Null
$msix = Join-Path $dir $msixAsset.name
$cer = Join-Path $dir $cerAsset.name
Write-Host "Downloading $($release.tag_name)..."
Invoke-WebRequest $msixAsset.browser_download_url -OutFile $msix -UseBasicParsing
Invoke-WebRequest $cerAsset.browser_download_url -OutFile $cer -UseBasicParsing

$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $cer
$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $cert.Thumbprint
if (-not $trusted) {
  Write-Host "Trusting signing certificate '$($cert.Subject)' (thumbprint $($cert.Thumbprint)). Approve the UAC prompt."
  $cmd = "Import-Certificate -FilePath '$cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"
  Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $cmd
}

Write-Host 'Installing package...'
Add-AppxPackage -Path $msix -ForceApplicationShutdown
Write-Host 'Installed.' -ForegroundColor Green

Write-Host ''
Write-Host 'Next: open Command Palette, reload it (or restart PowerToys), then right-click the Dock,'
Write-Host 'choose Edit and add the "Custom Dock Text" band. Click it to set your text.'
