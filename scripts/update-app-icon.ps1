param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ImagePath,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$iconAssetPath = Join-Path $repoRoot "src\Stt.App\Assets\whisper.ico"
$refreshScriptPath = Join-Path $PSScriptRoot "update-local-app.ps1"
$magickCommand = Get-Command magick -ErrorAction SilentlyContinue

if (-not $magickCommand) {
    throw "ImageMagick 'magick' was not found on PATH. Install ImageMagick or add it to PATH, then try again."
}

if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    throw "Image file not found: $ImagePath"
}

$resolvedImagePath = (Resolve-Path -LiteralPath $ImagePath).Path

& $magickCommand.Source `
    $resolvedImagePath `
    -background none `
    -define icon:auto-resize=16,20,24,32,40,48,64,128,256 `
    $iconAssetPath

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Updated app icon asset: $iconAssetPath"
Write-Host "Source image: $resolvedImagePath"

$refreshArgs = @{
    Configuration = $Configuration
    Runtime = $Runtime
}

if ($NoLaunch) {
    $refreshArgs.NoLaunch = $true
}

& $refreshScriptPath @refreshArgs
