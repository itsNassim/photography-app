<#
.SYNOPSIS
  Builds a Release, x86 portable folder build of PhotoStudioApp for Windows 7 SP1+.

.NOTES
  Run this from a "Developer PowerShell for VS" prompt (or plain PowerShell with
  msbuild.exe on PATH), from the repository root (the folder containing
  PhotoStudioApp.sln).

  Requirements on the BUILD machine (not the target Win7 machine):
    - Visual Studio 2019/2022 Build Tools (or full VS) with the
      ".NET desktop development" workload, which includes the
      .NET Framework 4.8 targeting pack.
    - NuGet.exe on PATH, or VS's built-in NuGet restore.

  The resulting bin\Release folder is a portable, xcopy-deployable build -
  copy it to the Windows 7 machine as-is (no installer required), or wrap it
  with Inno Setup / WiX if you want a proper installer.
#>

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x86"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Set-Location $root

Write-Host "== Restoring NuGet packages ==" -ForegroundColor Cyan
$toolsDir = Join-Path $root ".tools"
$localNuget = Join-Path $toolsDir "nuget.exe"
$nuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
if (-not $nuget -and (Test-Path $localNuget)) {
    $nuget = Get-Command $localNuget -ErrorAction SilentlyContinue
}
if (-not $nuget) {
    Write-Host "nuget.exe not found - downloading a local copy to .tools\nuget.exe ..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    try {
        Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $localNuget -UseBasicParsing
        $nuget = Get-Command $localNuget -ErrorAction SilentlyContinue
    } catch {
        Write-Warning "Could not download nuget.exe automatically ($($_.Exception.Message)). Continuing and hoping the package is already restored..."
    }
}
if ($nuget) {
    & $nuget restore PhotoStudioApp.sln
}

Write-Host "== Locating msbuild.exe ==" -ForegroundColor Cyan
$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if (-not $msbuild) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        $msbuildPath = Join-Path $vsPath "MSBuild\Current\Bin\msbuild.exe"
        if (Test-Path $msbuildPath) { $msbuild = $msbuildPath }
    }
}
if (-not $msbuild) {
    Write-Host ""
    Write-Host "msbuild.exe was not found anywhere on this machine - that means Visual" -ForegroundColor Red
    Write-Host "Studio / Build Tools with the .NET desktop workload isn't installed yet." -ForegroundColor Red
    Write-Host ""
    Write-Host "Fastest fix (Windows 10/11, PowerShell as Administrator):" -ForegroundColor Yellow
    Write-Host '  winget install --id Microsoft.VisualStudio.2022.BuildTools --silent --override "--wait --add Microsoft.VisualStudio.Workload.ManagedDesktop --includeRecommended"'
    Write-Host ""
    Write-Host "Or manually:" -ForegroundColor Yellow
    Write-Host "  1. Download 'Build Tools for Visual Studio 2022' (free) from:"
    Write-Host "     https://visualstudio.microsoft.com/downloads/  (scroll to 'Tools for Visual Studio')"
    Write-Host "  2. Run the installer, check the '.NET desktop build tools' workload, click Install."
    Write-Host "  3. Close and reopen PowerShell (so PATH/registry updates are picked up)."
    Write-Host "  4. Re-run: .\build\publish.ps1  (or .\build\build-installer.ps1)"
    Write-Host ""
    throw "msbuild.exe not found - install Visual Studio Build Tools (.NET desktop workload) as shown above, then re-run this script."
}

Write-Host "== Building $Configuration|$Platform ==" -ForegroundColor Cyan
& $msbuild "PhotoStudioApp.sln" "/p:Configuration=$Configuration" "/p:Platform=$Platform" "/t:Rebuild" "/m"

$outDir = "PhotoStudioApp\bin\$Configuration"
Write-Host ""
Write-Host "Build complete. Portable build is at: $outDir" -ForegroundColor Green
Write-Host "Copy that whole folder to the Windows 7 (or later) machine and run PhotoStudioApp.exe." -ForegroundColor Green
Write-Host "Make sure the folder retains the x86\SQLite.Interop.dll and x64\SQLite.Interop.dll subfolders NuGet placed next to the exe." -ForegroundColor Yellow
