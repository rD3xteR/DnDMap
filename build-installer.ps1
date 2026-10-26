param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$Runtime = "win-x64",
    [switch]$SkipClean
)

# Builds DnDMap, publishes it as a self-contained app and compiles the
# Inno Setup installer (DnDMap.iss) into Setup\DnDMap-Setup.exe.
#
# Requirements: .NET SDK (see global.json), Inno Setup 6 (ISCC.exe).
# ASCII-only on purpose: the script must survive any console code page.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

$sln  = Join-Path $root "DnDMap.sln"
$proj = Join-Path $root "DnDMap\DnDMap.csproj"
$iss  = Join-Path $root "DnDMap.iss"

$publishDir = Join-Path $root "publish\$Version"
$setupDir   = Join-Path $root "Setup"
$setupExe   = Join-Path $setupDir "DnDMap-Setup.exe"

function Invoke-DotNet {
    param([string[]]$DotNetArgs)
    Write-Host ">> dotnet $($DotNetArgs -join ' ')" -ForegroundColor Cyan
    & dotnet @DotNetArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: dotnet exited with code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

function Find-Iscc {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }
    $fromPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }
    return $null
}

# --- 1. Clean previous artifacts -------------------------------------------

foreach ($dir in @($setupDir, $publishDir)) {
    if (Test-Path $dir) {
        Write-Host ">> removing $dir" -ForegroundColor Cyan
        Remove-Item $dir -Recurse -Force
    }
}

# --- 2. Restore / build -----------------------------------------------------

if (-not $SkipClean) {
    Invoke-DotNet -DotNetArgs @('clean', $sln, '-c', $Configuration)
}
Invoke-DotNet -DotNetArgs @('restore', $sln)
Invoke-DotNet -DotNetArgs @('build', $sln, '-c', $Configuration, '--no-restore', '--no-incremental')

# --- 3. Publish (self-contained, no .NET runtime needed on the target) ------

Invoke-DotNet -DotNetArgs @('publish', $proj, '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true', '-o', $publishDir)
if (-not (Test-Path (Join-Path $publishDir "DnDMap.exe"))) {
    Write-Host "ERROR: publish output not found: $publishDir\DnDMap.exe" -ForegroundColor Red
    exit 1
}

# --- 4. Compile the installer ----------------------------------------------

$iscc = Find-Iscc
if (-not $iscc) {
    Write-Host "ERROR: Inno Setup 6 (ISCC.exe) not found." -ForegroundColor Red
    Write-Host "Install it from https://jrsoftware.org/isdl.php or run: choco install innosetup" -ForegroundColor Red
    exit 1
}

# PublishDir is passed relative to this script, so ISCC resolves it against
# the directory of DnDMap.iss regardless of the current working directory.
Write-Host ">> $iscc /DMyAppVersion=$Version $iss" -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$Version" "/DPublishDir=publish\$Version" $iss
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: ISCC.exe exited with code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

if (-not (Test-Path $setupExe)) {
    Write-Host "ERROR: installer was not created: $setupExe" -ForegroundColor Red
    exit 1
}

$setupSize  = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
$publishSize = [math]::Round((Get-ChildItem $publishDir -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)

Write-Host ""
Write-Host "Published app : $publishDir ($publishSize MB)" -ForegroundColor Green
Write-Host "Installer     : $setupExe ($setupSize MB)" -ForegroundColor Green
