# Builds dist\Switch2ProBridge-Setup-<version>.exe: publishes the app, fetches the ViGEmBus installer, runs Inno Setup.
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$vigemFile = "ViGEmBus_1.22.0_x64_x86_arm64.exe"
$vigemUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/$vigemFile"
# Pinned so a swapped download can't end up inside the installer.
$vigemSha256 = "89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A"
$redist = "installer\redist"

New-Item -ItemType Directory -Force $redist | Out-Null
if (-not (Test-Path "$redist\$vigemFile")) {
    Invoke-WebRequest $vigemUrl -OutFile "$redist\$vigemFile"
}
if ((Get-FileHash "$redist\$vigemFile" -Algorithm SHA256).Hash -ne $vigemSha256) {
    throw "$vigemFile does not match the pinned SHA-256. Delete it and re-run, or update the pin after checking the new file."
}
if (-not (Test-Path "$redist\ViGEmBus-LICENSE.txt")) {
    Invoke-WebRequest "https://raw.githubusercontent.com/nefarius/ViGEmBus/master/LICENSE" -OutFile "$redist\ViGEmBus-LICENSE.txt"
}

dotnet publish Switch2ProBridge.App -c Release -o publish -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

& $iscc "/DAppVersion=$Version" installer\Switch2ProBridge.iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
