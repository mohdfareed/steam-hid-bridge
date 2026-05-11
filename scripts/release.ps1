<#
.SYNOPSIS
Packages a release zip, or runs the local tag-and-push release gate with -TagRelease.
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$TagRelease,
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (!$TagRelease) {
    & (Join-Path $root "scripts\internal\package.ps1") -Configuration $Configuration -Runtime $Runtime -Version $Version
    exit $LASTEXITCODE
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Label,

        [Parameter(Mandatory = $true)]
        [scriptblock] $Command
    )

    Write-Host ""
    Write-Host "==> $Label"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Assert-CleanWorkTree {
    $status = git status --porcelain
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "Working tree is not clean. Commit or stash changes before tagging a release."
    }
}

function Get-LatestVersionTag {
    $tags = git tag --list "v[0-9]*.[0-9]*.[0-9]*" --sort=-v:refname
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    foreach ($tag in $tags) {
        if ($tag -match '^v\d+\.\d+\.\d+$') {
            return $tag
        }
    }

    return "<none>"
}

git rev-parse --is-inside-work-tree | Out-Null
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$latestTag = Get-LatestVersionTag
Write-Host "Latest version tag: $latestTag"

$versionInput = Read-Host "Next version (example: 0.1.1 or v0.1.1)"
$versionInput = $versionInput.Trim()
if ([string]::IsNullOrWhiteSpace($versionInput)) {
    throw "Version is required."
}

$tag = if ($versionInput.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) {
    "v$($versionInput.Substring(1))"
}
else {
    "v$versionInput"
}

if ($tag -notmatch '^v\d+\.\d+\.\d+$') {
    throw "Version must match vMAJOR.MINOR.PATCH, for example v0.1.1."
}

$version = $tag.Substring(1)

git rev-parse --verify --quiet "refs/tags/$tag" | Out-Null
$localTagExitCode = $LASTEXITCODE
if ($localTagExitCode -eq 0) {
    throw "Tag already exists locally: $tag"
}
if ($localTagExitCode -ne 1) {
    exit $localTagExitCode
}

Invoke-Checked "Fetch tags" { git fetch --tags }

git ls-remote --exit-code --tags origin "refs/tags/$tag" | Out-Null
$remoteTagExitCode = $LASTEXITCODE
if ($remoteTagExitCode -eq 0) {
    throw "Tag already exists on origin: $tag"
}
if ($remoteTagExitCode -ne 2) {
    exit $remoteTagExitCode
}

Assert-CleanWorkTree

Invoke-Checked "Build" { & (Join-Path $root "scripts\check.ps1") -Configuration Release }
Invoke-Checked "Package" { & (Join-Path $root "scripts\internal\package.ps1") -Configuration Release -Runtime $Runtime -Version $version }

Assert-CleanWorkTree

Write-Host ""
Write-Host "Ready to create and push $tag from commit:"
git log -1 --oneline
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$confirmation = Read-Host "Create and push this tag to origin? Type 'yes' to continue"
if ($confirmation -ne "yes") {
    Write-Host "Canceled. No tag was created."
    exit 0
}

Invoke-Checked "Create tag" { git tag $tag }
Invoke-Checked "Push tag" { git push origin $tag }

Write-Host ""
Write-Host "Pushed $tag. The GitHub release workflow should start from the tag push."
