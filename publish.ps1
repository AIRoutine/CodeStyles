<#
.SYNOPSIS
    Increments minor version and publishes all NuGet packages.

.DESCRIPTION
    This script:
    1. Reads current version from csproj files
    2. Increments the minor version (1.0.0 -> 1.1.0)
    3. Updates all csproj files
    4. Packs all projects
    5. Pushes to NuGet.org

.PARAMETER Major
    Increment major version instead of minor (1.0.0 -> 2.0.0)

.PARAMETER Patch
    Increment patch version instead of minor (1.0.0 -> 1.0.1)

.PARAMETER NuGetApiKey
    NuGet API key. If not provided, uses NUGET_API_KEY environment variable.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Patch
    .\publish.ps1 -Major
    .\publish.ps1 -NuGetApiKey "your-api-key"
#>

param(
    [switch]$Major,
    [switch]$Patch,
    [string]$NuGetApiKey
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $scriptDir "src"
$packagesDir = Join-Path $scriptDir "packages"

$projects = @(
    "AIRoutine.CodeStyle.Common",
    "AIRoutine.CodeStyle.AspNetCore",
    "AIRoutine.CodeStyle.Uno"
)

# Get API key
if (-not $NuGetApiKey) {
    $NuGetApiKey = $env:NUGET_API_KEY
}

if (-not $NuGetApiKey) {
    Write-Error "NuGet API key not provided. Use -NuGetApiKey parameter or set NUGET_API_KEY environment variable."
    exit 1
}

# Read current version from first project
$commonCsproj = Join-Path $srcDir "AIRoutine.CodeStyle.Common\AIRoutine.CodeStyle.Common.csproj"
[xml]$xml = Get-Content $commonCsproj
$currentVersion = $xml.Project.PropertyGroup.Version
Write-Host "Current version: $currentVersion" -ForegroundColor Cyan

# Parse version
$versionParts = $currentVersion.Split('.')
$majorVer = [int]$versionParts[0]
$minorVer = [int]$versionParts[1]
$patchVer = [int]$versionParts[2]

# Increment version
if ($Major) {
    $majorVer++
    $minorVer = 0
    $patchVer = 0
} elseif ($Patch) {
    $patchVer++
} else {
    # Default: minor
    $minorVer++
    $patchVer = 0
}

$newVersion = "$majorVer.$minorVer.$patchVer"
Write-Host "New version: $newVersion" -ForegroundColor Green

# Update all csproj files
foreach ($project in $projects) {
    $csprojPath = Join-Path $srcDir "$project\$project.csproj"
    Write-Host "Updating $project to version $newVersion..." -ForegroundColor Yellow

    [xml]$xml = Get-Content $csprojPath
    $xml.Project.PropertyGroup.Version = $newVersion
    $xml.Save($csprojPath)
}

# Clean packages directory
if (Test-Path $packagesDir) {
    Remove-Item "$packagesDir\*.nupkg" -Force
}

# Pack all projects
Write-Host "`nPacking projects..." -ForegroundColor Cyan
foreach ($project in $projects) {
    $csprojPath = Join-Path $srcDir "$project\$project.csproj"
    Write-Host "Packing $project..." -ForegroundColor Yellow

    dotnet pack $csprojPath -c Release -o $packagesDir --no-build
    if ($LASTEXITCODE -ne 0) {
        # Try with build
        dotnet pack $csprojPath -c Release -o $packagesDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to pack $project"
            exit 1
        }
    }
}

# Push to NuGet
Write-Host "`nPushing to NuGet.org..." -ForegroundColor Cyan
$nupkgs = Get-ChildItem "$packagesDir\*.nupkg"

foreach ($nupkg in $nupkgs) {
    Write-Host "Pushing $($nupkg.Name)..." -ForegroundColor Yellow
    dotnet nuget push $nupkg.FullName --api-key $NuGetApiKey --source https://api.nuget.org/v3/index.json --skip-duplicate
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to push $($nupkg.Name)"
        exit 1
    }
}

Write-Host "`nSuccessfully published version $newVersion!" -ForegroundColor Green
Write-Host "Packages:" -ForegroundColor Cyan
foreach ($project in $projects) {
    Write-Host "  - https://www.nuget.org/packages/$project/$newVersion" -ForegroundColor White
}
