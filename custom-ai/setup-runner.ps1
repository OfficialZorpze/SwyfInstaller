[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [string]$Tag = "custom-ai-v1.1.6",

    [string]$RunnerDir = (Join-Path $env:USERPROFILE "actions-runner-swyf")
)

$ErrorActionPreference = "Stop"
$Repo = "OfficialZorpze/SwyfInstaller"
$RepoUrl = "https://github.com/$Repo"

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required. Install it and sign in before rerunning this script."
    }
}

Assert-Command "gh"
Assert-Command "git"

$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$ManagedDir = Join-Path $GameDir "Scam With Your Friends_Data/Managed"
if (-not (Test-Path -LiteralPath $ManagedDir -PathType Container)) {
    throw "That folder does not contain Scam With Your Friends_Data/Managed: $GameDir"
}
if ($Tag -notmatch '^custom-ai-v[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Tag must use custom-ai-vMAJOR.MINOR.PATCH format."
}

gh auth status
if ($LASTEXITCODE -ne 0) {
    throw "Sign in first with: gh auth login"
}

Write-Host "Setting the repository's SWYF_GAME_DIR Actions variable..."
gh variable set SWYF_GAME_DIR --repo $Repo --body $GameDir
if ($LASTEXITCODE -ne 0) {
    throw "Could not set SWYF_GAME_DIR. Your GitHub token needs repository administration/Actions variable permissions."
}

if (-not (Test-Path -LiteralPath $RunnerDir)) {
    New-Item -ItemType Directory -Path $RunnerDir -Force | Out-Null
}

$RunnerConfigured = (Test-Path -LiteralPath (Join-Path $RunnerDir ".runner")) -and
                    (Test-Path -LiteralPath (Join-Path $RunnerDir "run.cmd"))

if ($RunnerConfigured) {
    Write-Host "An existing GitHub Actions runner is already configured in $RunnerDir."
    Write-Host "Skipping download and registration; the existing runner will be started."
}
else {
    if (Get-ChildItem -LiteralPath $RunnerDir -Force -ErrorAction SilentlyContinue | Select-Object -First 1) {
        throw "Runner directory contains files but is not configured. Inspect $RunnerDir; do not delete it unless you have confirmed it is safe to do so."
    }

    Write-Host "Getting a one-time GitHub runner registration token..."
    $RegistrationToken = gh api --method POST "repos/$Repo/actions/runners/registration-token" --jq .token
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RegistrationToken)) {
        throw "Could not obtain a runner registration token. Your GitHub account/token must have repository admin access."
    }

    Write-Host "Finding the latest official Windows x64 Actions runner..."
    $Release = Invoke-RestMethod -Uri "https://api.github.com/repos/actions/runner/releases/latest"
    $Asset = $Release.assets | Where-Object { $_.name -match '^actions-runner-win-x64-.*\\.zip$' } | Select-Object -First 1
    if (-not $Asset) { throw "Could not find the Windows x64 runner ZIP in the latest runner release." }

    $ZipPath = Join-Path $env:TEMP $Asset.name
    Invoke-WebRequest -Uri $Asset.browser_download_url -OutFile $ZipPath
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $RunnerDir -Force
    Remove-Item -LiteralPath $ZipPath -Force

    Push-Location $RunnerDir
    try {
        Write-Host "Registering this PC as a self-hosted runner..."
        & .\config.cmd --url $RepoUrl --token $RegistrationToken --name "$env:COMPUTERNAME-swyf" --labels "swyf-game" --work "_work" --unattended --replace
        if ($LASTEXITCODE -ne 0) { throw "Runner registration failed." }
    } finally {
        Pop-Location
    }
    $RegistrationToken = $null
}
Write-Host ""
Write-Host "Runner is configured. Starting it now; keep this PowerShell window open."
Write-Host "The queued GitHub Actions job will build and publish the release."
Push-Location $RunnerDir
try {
    & .\run.cmd
} finally {
    Pop-Location
}
