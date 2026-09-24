param(
    [string]$Arch = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$PackageName = "Clipboard-$Version-$Arch"
$OutputDir = "dist-$Arch"
$ZipName = "$PackageName.zip"

Write-Host "==> Building Clipboard for Windows ($Arch, v$Version)..."

# 1. Publish Native AOT with fallback
try {
    dotnet publish -c Release -r $Arch -p:PublishAot=true -o "$OutputDir/$PackageName"
    if ($LASTEXITCODE -ne 0) { throw "AOT compilation returned non-zero exit code" }
} catch {
    Write-Host "==> Native AOT failed, falling back to trimmed publish..."
    dotnet publish -c Release -r $Arch --self-contained true -p:PublishTrimmed=true -o "$OutputDir/$PackageName"
}

# 2. Create Zip Archive
Write-Host "==> Creating $ZipName..."
if (Test-Path $ZipName) {
    Remove-Item $ZipName -Force
}

Compress-Archive -Path "$OutputDir/$PackageName/*" -DestinationPath $ZipName -Force

Write-Host "==> Successfully created: $ZipName"
