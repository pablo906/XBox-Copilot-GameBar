# Called by the Setup.exe installer as the signed-in user. Installs the widget
# package with the framework packages it depends on.
param(
  [Parameter(Mandatory)] [string]$PackageDir
)

$ErrorActionPreference = 'Stop'
Start-Transcript -Path (Join-Path $env:TEMP 'CopilotGameBarBridge-install.log') -Force | Out-Null
try {
  $package = Get-ChildItem $PackageDir -Filter *.msix | Select-Object -First 1
  $deps = @(Get-ChildItem (Join-Path $PackageDir 'deps') -Filter *.appx | ForEach-Object FullName)
  Write-Host "Installing $($package.Name) with $($deps.Count) dependencies"
  Add-AppxPackage -Path $package.FullName -DependencyPath $deps -ForceApplicationShutdown
  Write-Host 'Installed.'
  exit 0
} catch {
  Write-Host $_
  exit 1
} finally {
  Stop-Transcript | Out-Null
}
