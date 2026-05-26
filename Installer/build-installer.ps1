param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "BabyShop.csproj"
$publishDir = Join-Path $PSScriptRoot "Publish\$Runtime"
$payloadDir = Join-Path $PSScriptRoot "Payload"
$outputDir = Join-Path $PSScriptRoot "Output"
$bootstrapperDir = Join-Path $PSScriptRoot "Bootstrapper"
$packageZip = Join-Path $payloadDir "BabyShopPackage.zip"
$targetExe = Join-Path $outputDir "BabyShopSetup.exe"
$sourceFile = Join-Path $bootstrapperDir "BabyShopInstaller.cs"
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

New-Item -ItemType Directory -Force -Path $publishDir, $payloadDir, $outputDir, $bootstrapperDir | Out-Null

dotnet publish $projectFile `
  -c $Configuration `
  -r $Runtime `
  --no-restore `
  --self-contained true `
  -p:UseAppHost=true `
  -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Application publish failed with exit code $LASTEXITCODE."
}

if (Test-Path $packageZip) {
    Remove-Item $packageZip -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $packageZip -Force

if (Test-Path $targetExe) {
    Remove-Item $targetExe -Force
}

$cscArgs = @(
    "/target:winexe",
    "/nologo",
    "/optimize",
    "/out:$targetExe",
    "/resource:$packageZip,BabyShop.Payload.Zip",
    "/r:System.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.IO.Compression.FileSystem.dll",
    $sourceFile
)

& $cscPath $cscArgs

if ($LASTEXITCODE -ne 0) {
    throw "Installer bootstrapper compilation failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $targetExe)) {
    throw "Installer file was not created: $targetExe"
}

Write-Host "Installer created:" $targetExe
