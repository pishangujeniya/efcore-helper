<#
.SYNOPSIS
    Builds the EF Core Helper Visual Studio Extension, runs unit tests, and packages the VSIX.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.
.PARAMETER Version
    Version string for the extension and assemblies (e.g. 1.0.0 or 1.0.1).
.PARAMETER Publisher
    Marketplace publisher name to stamp into the manifest (optional).
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$Publisher = ""
)

$ErrorActionPreference = "Stop"

# Extract version from Git tag or env if not passed
if (-not $Version) {
    if ($env:GITHUB_REF_NAME -and $env:GITHUB_REF_NAME -match '^v?(\d+\.\d+\.\d+.*)$') {
        $Version = $Matches[1]
    } elseif ($env:APP_VERSION) {
        $Version = $env:APP_VERSION
    }
}

# Clean version (strip leading 'v')
if ($Version) {
    $Version = $Version -replace '^v', ''
}

# Read current version from manifest if still empty
$manifestPath = Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\source.extension.vsixmanifest"
$manifestXml = [xml](Get-Content $manifestPath)
if (-not $Version) {
    $Version = $manifestXml.PackageManifest.Metadata.Identity.Version
}

# Stamp version into vsixmanifest if explicitly changed
if ($Version -and $manifestXml.PackageManifest.Metadata.Identity.Version -ne $Version) {
    Write-Host "Updating vsixmanifest version to $Version..." -ForegroundColor Cyan
    $manifestXml.PackageManifest.Metadata.Identity.Version = $Version
    $manifestXml.Save($manifestPath)
}

# Stamp publisher into vsixmanifest and publishManifest.json if passed
if ($Publisher) {
    Write-Host "Updating publisher to $Publisher..." -ForegroundColor Cyan
    $manifestXml.PackageManifest.Metadata.Identity.Publisher = $Publisher
    $manifestXml.Save($manifestPath)

    $publishManifestPath = Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\publishManifest.json"
    if (Test-Path $publishManifestPath) {
        $json = Get-Content $publishManifestPath | ConvertFrom-Json
        $json.publisher = $Publisher
        $json | ConvertTo-Json -Depth 10 | Set-Content $publishManifestPath
    }
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Building EF Core Helper for Visual Studio" -ForegroundColor Cyan
Write-Host "  Configuration : $Configuration" -ForegroundColor Cyan
Write-Host "  Version       : $Version" -ForegroundColor Cyan
if ($Publisher) {
    Write-Host "  Publisher     : $Publisher" -ForegroundColor Cyan
}
Write-Host "================================================================" -ForegroundColor Cyan

# 1. Locate MSBuild
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = $null
if (Test-Path $vswhere) {
    $vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if ($vsPath) {
        $candidate = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path $candidate) {
            $msbuild = $candidate
        }
    }
}

if (-not $msbuild) {
    throw "MSBuild.exe could not be found. Please ensure Visual Studio is installed."
}

Write-Host "[1/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore EFCoreHelper.sln

Write-Host "[2/5] Running automated tests..." -ForegroundColor Yellow
dotnet test tests/EFCoreHelper.Core.Tests/EFCoreHelper.Core.Tests.csproj --configuration $Configuration --no-restore

Write-Host "[3/5] Building Core and Sample projects..." -ForegroundColor Yellow
dotnet build EFCoreHelper.sln --configuration $Configuration --no-restore -p:Version=$Version

Write-Host "[4/5] Building Visual Studio Extension..." -ForegroundColor Yellow
& $msbuild src\EFCoreHelper.Vsix\EFCoreHelper.Vsix.csproj -t:Rebuild -p:Configuration=$Configuration -p:Version=$Version -p:AssemblyVersion=$Version -p:FileVersion=$Version

# 5. Package VSIX
Write-Host "[5/5] Packaging VSIX container..." -ForegroundColor Yellow
$artifactsDir = Join-Path $PSScriptRoot "artifacts"
if (-not (Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Path $artifactsDir | Out-Null
}

$vsixUtil = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.vssdk.buildtools\17.10.2185\tools\vssdk\bin\VsixUtil.exe"
$createPkgDef = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.vssdk.buildtools\17.10.2185\tools\vssdk\bin\CreatePkgDef.exe"

$outputVsix = Join-Path $artifactsDir "EFCoreHelper.vsix"
$manifest = Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\source.extension.vsixmanifest"
$vsixBin = Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\bin\$Configuration\net48"
$pkgDef = Join-Path $vsixBin "EFCoreHelper.Vsix.pkgdef"

if (Test-Path $createPkgDef) {
    & $createPkgDef /out="$pkgDef" /codebase "$vsixBin\EFCoreHelper.Vsix.dll"
}

if (Test-Path $vsixUtil) {
    & $vsixUtil package -outputPath "$outputVsix" -sourceManifest "$manifest" -noValidate
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName WindowsBase

$zip = [System.IO.Compression.ZipFile]::Open($outputVsix, [System.IO.Compression.ZipArchiveMode]::Update)

# Ensure [Content_Types].xml contains all required MIME types for VSIX parts
$ctEntry = $zip.GetEntry('[Content_Types].xml')
if ($ctEntry) { $ctEntry.Delete() }
$newCtEntry = $zip.CreateEntry('[Content_Types].xml')
$ctStream = $newCtEntry.Open()
$ctWriter = [System.IO.StreamWriter]::new($ctStream, [System.Text.Encoding]::UTF8)
$ctWriter.Write('<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="vsixmanifest" ContentType="text/xml" /><Default Extension="json" ContentType="application/json" /><Default Extension="txt" ContentType="text/plain" /><Default Extension="pkgdef" ContentType="text/plain" /><Default Extension="dll" ContentType="application/octet-stream" /><Default Extension="png" ContentType="image/png" /></Types>')
$ctWriter.Dispose()
$ctStream.Dispose()

function AddOrUpdateEntry($archive, $source, $entry) {
    if (Test-Path $source) {
        $existing = $archive.GetEntry($entry)
        if ($existing -ne $null) { $existing.Delete() }
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $entry) | Out-Null
    }
}

AddOrUpdateEntry $zip "$vsixBin\EFCoreHelper.Vsix.dll" "EFCoreHelper.Vsix.dll"
AddOrUpdateEntry $zip "$pkgDef" "EFCoreHelper.Vsix.pkgdef"
AddOrUpdateEntry $zip "$vsixBin\EFCoreHelper.Core.dll" "EFCoreHelper.Core.dll"
AddOrUpdateEntry $zip "$vsixBin\Community.VisualStudio.Toolkit.dll" "Community.VisualStudio.Toolkit.dll"
AddOrUpdateEntry $zip "$vsixBin\System.Text.Json.dll" "System.Text.Json.dll"
AddOrUpdateEntry $zip "$vsixBin\Microsoft.Bcl.AsyncInterfaces.dll" "Microsoft.Bcl.AsyncInterfaces.dll"
AddOrUpdateEntry $zip (Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\Resources\Icon.png") "Resources/Icon.png"
AddOrUpdateEntry $zip (Join-Path $PSScriptRoot "LICENSE.txt") "LICENSE.txt"

$zip.Dispose()

# Validate package parts using OPC (Open Packaging Convention / System.IO.Packaging)
$pkgStream = [System.IO.File]::Open($outputVsix, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
$pkg = [System.IO.Packaging.Package]::Open($pkgStream)
$requiredParts = @('/extension.vsixmanifest', '/LICENSE.txt', '/Resources/Icon.png', '/EFCoreHelper.Vsix.dll', '/EFCoreHelper.Vsix.pkgdef')
foreach ($part in $requiredParts) {
    if (-not $pkg.PartExists([System.Uri]::new($part, [System.UriKind]::Relative))) {
        $pkg.Close()
        $pkgStream.Close()
        throw "VSIX validation failed: Part '$part' not found or has invalid content type in package."
    }
}
$pkg.Close()
$pkgStream.Close()

$size = (Get-Item $outputVsix).Length / 1KB
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  Build Succeeded!" -ForegroundColor Green
Write-Host "  Version  : $Version" -ForegroundColor Green
Write-Host "  Artifact : $outputVsix ($([math]::Round($size, 1)) KB)" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
