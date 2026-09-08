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

$nugetPackages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
$vsixUtil = Join-Path $nugetPackages "microsoft.vssdk.buildtools\17.10.2185\tools\vssdk\bin\VsixUtil.exe"
$createPkgDef = Join-Path $nugetPackages "microsoft.vssdk.buildtools\17.10.2185\tools\vssdk\bin\CreatePkgDef.exe"
$schemaDir = Join-Path $nugetPackages "microsoft.vssdk.buildtools\17.10.2185\tools\vssdk\schemas"

if (-not (Test-Path $vsixUtil)) {
    $found = Get-ChildItem -Path (Join-Path $nugetPackages "microsoft.vssdk.buildtools") -Filter "VsixUtil.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) {
        $vsixUtil = $found.FullName
        $createPkgDef = Join-Path (Split-Path $vsixUtil) "CreatePkgDef.exe"
        $schemaDir = Join-Path (Split-Path (Split-Path $vsixUtil)) "schemas"
    }
}

if (-not (Test-Path $vsixUtil)) {
    throw "VsixUtil.exe could not be found in Microsoft.VSSDK.BuildTools NuGet package."
}

$outputVsix = Join-Path $artifactsDir "EFCoreHelper.vsix"
$manifest = Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\source.extension.vsixmanifest"
$vsixBin = Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\bin\$Configuration\net48"
$pkgDef = Join-Path $vsixBin "EFCoreHelper.Vsix.pkgdef"

if (Test-Path $createPkgDef) {
    Write-Host "Generating pkgdef file..." -ForegroundColor Cyan
    & $createPkgDef /out="$pkgDef" /codebase "$vsixBin\EFCoreHelper.Vsix.dll"
    if ($LASTEXITCODE -ne 0) {
        throw "CreatePkgDef failed with exit code $LASTEXITCODE."
    }
}

# Construct files manifest for VsixUtil
$filesJsonPath = Join-Path $artifactsDir "vsix-files.json"
$filesToInclude = @(
    @{ path = "$vsixBin\EFCoreHelper.Vsix.dll"; targetPath = "EFCoreHelper.Vsix.dll"; vsixSubPath = "" },
    @{ path = "$pkgDef"; targetPath = "EFCoreHelper.Vsix.pkgdef"; vsixSubPath = "" },
    @{ path = "$vsixBin\EFCoreHelper.Core.dll"; targetPath = "EFCoreHelper.Core.dll"; vsixSubPath = "" },
    @{ path = "$vsixBin\Community.VisualStudio.Toolkit.dll"; targetPath = "Community.VisualStudio.Toolkit.dll"; vsixSubPath = "" },
    @{ path = "$vsixBin\System.Text.Json.dll"; targetPath = "System.Text.Json.dll"; vsixSubPath = "" },
    @{ path = "$vsixBin\System.Text.Encodings.Web.dll"; targetPath = "System.Text.Encodings.Web.dll"; vsixSubPath = "" },
    @{ path = "$vsixBin\Microsoft.Bcl.AsyncInterfaces.dll"; targetPath = "Microsoft.Bcl.AsyncInterfaces.dll"; vsixSubPath = "" },
    @{ path = (Join-Path $PSScriptRoot "src\EFCoreHelper.Vsix\Resources\Icon.png"); targetPath = "Icon.png"; vsixSubPath = "Resources" },
    @{ path = (Join-Path $PSScriptRoot "LICENSE.txt"); targetPath = "LICENSE.txt"; vsixSubPath = "" }
)

$fileEntries = @()
foreach ($item in $filesToInclude) {
    if (-not (Test-Path $item.path)) {
        throw "Required file not found for VSIX packaging: $($item.path)"
    }
    $fileEntries += @{
        culture = ""
        installRoot = ""
        ngen = $null
        path = (Get-Item $item.path).FullName
        targetPath = $item.targetPath
        vsixSubPath = $item.vsixSubPath
    }
}

@{ files = $fileEntries } | ConvertTo-Json -Depth 5 | Set-Content $filesJsonPath -Encoding UTF8

if (Test-Path $outputVsix) {
    Remove-Item $outputVsix -Force
}

$vsixArgs = @(
    "package",
    "-outputPath", "$outputVsix",
    "-sourceManifest", "$manifest",
    "-files", "$filesJsonPath",
    "-setupProductArch", "amd64",
    "-is64BitBuild"
)
if (Test-Path $schemaDir) {
    $vsixArgs += "-vsixSchemaPath"
    $vsixArgs += "$schemaDir"
}

Write-Host "Creating VSIX package with VsixUtil..." -ForegroundColor Cyan
& $vsixUtil @vsixArgs
if ($LASTEXITCODE -ne 0) {
    throw "VsixUtil package failed with exit code $LASTEXITCODE."
}

# Clean up temporary files manifest
if (Test-Path $filesJsonPath) {
    Remove-Item $filesJsonPath -Force
}

# Validate package parts using OPC (Open Packaging Convention / System.IO.Packaging)
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.IO.Compression.FileSystem

$pkgStream = [System.IO.File]::Open($outputVsix, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
$pkg = [System.IO.Packaging.Package]::Open($pkgStream)

$requiredParts = @(
    '/extension.vsixmanifest',
    '/manifest.json',
    '/catalog.json',
    '/LICENSE.txt',
    '/Resources/Icon.png',
    '/EFCoreHelper.Vsix.dll',
    '/EFCoreHelper.Vsix.pkgdef',
    '/EFCoreHelper.Core.dll',
    '/Community.VisualStudio.Toolkit.dll',
    '/System.Text.Json.dll',
    '/System.Text.Encodings.Web.dll',
    '/Microsoft.Bcl.AsyncInterfaces.dll'
)

foreach ($part in $requiredParts) {
    if (-not $pkg.PartExists([System.Uri]::new($part, [System.UriKind]::Relative))) {
        $pkg.Close()
        $pkgStream.Close()
        throw "VSIX validation failed: Part '$part' not found in package."
    }
}
$pkg.Close()
$pkgStream.Close()

# Verify manifest.json contains all files
$zip = [System.IO.Compression.ZipFile]::OpenRead($outputVsix)
$manifestEntry = $zip.GetEntry("manifest.json")
$manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
$manifestContent = $manifestReader.ReadToEnd()
$manifestReader.Dispose()
$manifestData = $manifestContent | ConvertFrom-Json

$manifestFileNames = $manifestData.files | ForEach-Object { $_.fileName }
foreach ($part in $requiredParts) {
    if ($part -notin @('/manifest.json', '/catalog.json')) {
        if ($part -notin $manifestFileNames) {
            $zip.Dispose()
            throw "Package manifest validation failed: Part '$part' is not listed in manifest.json."
        }
    }
}
$zip.Dispose()

$size = (Get-Item $outputVsix).Length / 1KB
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  Build Succeeded!" -ForegroundColor Green
Write-Host "  Version  : $Version" -ForegroundColor Green
Write-Host "  Artifact : $outputVsix ($([math]::Round($size, 1)) KB)" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
