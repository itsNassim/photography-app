<#
.SYNOPSIS
  Builds PhotoStudioApp (Release, x86) and then compiles it into a single
  Windows installer (installer\Output\PhotoStudioApp-Setup.exe) using Inno
  Setup's command-line compiler (ISCC.exe).

.NOTES
  Run from a "Developer PowerShell for VS" prompt, from the repository root
  (the folder containing PhotoStudioApp.sln).

  Requirements on the BUILD machine (not the target Windows 7 machine):
    - Visual Studio 2019/2022 Build Tools (or full VS) with the
      ".NET desktop development" workload. The .NET Framework 4.6 targeting
      pack does NOT need to be installed separately - publish.ps1 (called
      below) downloads the Microsoft.NETFramework.ReferenceAssemblies.net46
      NuGet package directly instead. See README.md for details.
    - NuGet.exe on PATH (or packages already restored via Visual Studio once).
    - Inno Setup 6, free: https://jrsoftware.org/isinfo.php
      (installs ISCC.exe, normally at
      "C:\Program Files (x86)\Inno Setup 6\ISCC.exe")
#>

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Set-Location $root

# --- Step 1: build the app (reuses publish.ps1's build steps) ---
Write-Host "== Building the app (Release|x86) ==" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "publish.ps1")

# --- Step 2: find ISCC.exe ---
Write-Host ""
Write-Host "== Locating ISCC.exe (Inno Setup compiler) ==" -ForegroundColor Cyan
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $iscc = $c; break }
    }
}
if (-not $iscc) {
    throw "Could not find ISCC.exe. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php (default install adds it to Program Files (x86)\Inno Setup 6), then re-run this script."
}

# --- Step 3: compile the installer ---
Write-Host ""
Write-Host "== Compiling installer ==" -ForegroundColor Cyan
& $iscc "installer\PhotoStudioApp.iss"

Write-Host ""
Write-Host "Done. Installer is at: installer\Output\PhotoStudioApp-Setup.exe" -ForegroundColor Green
Write-Host "Copy that single file to the target Windows 7+ machine and run it." -ForegroundColor Green
