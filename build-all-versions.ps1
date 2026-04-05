param(
    [string]$RepoRoot = ".",
    [switch]$SkipUi,
    [switch]$NoMerge
)

$ErrorActionPreference = "Stop"

# --- Configuration ------------------------------------------------------------

$branches = @(
    @{ Branch = "master"; Version = "1.29.1" },
    @{ Branch = "ssl-tauri-discord-1.34.2"; Version = "1.34.2" },
    # @{ Branch = "ssl-tauri-discord-1.37.0"; Version = "1.37.0" },
    @{ Branch = "ssl-tauri-discord-1.39.1"; Version = "1.39.1" },
    @{ Branch = "ssl-tauri-discord-1.40.8"; Version = "1.40.8" },
    @{ Branch = "ssl-tauri-discord-1.41.1"; Version = "1.41.1" },
    @{ Branch = "ssl-tauri-discord-1.42.0"; Version = "1.42.0" }
)

$repoRoot = (Resolve-Path $RepoRoot).Path
$uiDir = Join-Path $repoRoot "TournamentAssistantUI"
$pluginDir = Join-Path $repoRoot "TournamentAssistant"
$pluginProject = Join-Path $pluginDir "TournamentAssistant.csproj"
$pluginUserFile = Join-Path $pluginDir "TournamentAssistant.csproj.user"
$artifactsRoot = Join-Path $repoRoot "Artifacts"

# Adjust if your Beat Saber installations live somewhere else
$beatSaberBaseDir = "O:\BSManager\BSInstances"

# Tauri output locations
$uiWebBuildDir = Join-Path $uiDir "build"
$tauriExePath = Join-Path $uiDir "src-tauri\target\release\taui.exe"

# Plugin output guesses. We'll probe these in order.
$pluginOutputCandidates = @(
    (Join-Path $pluginDir "bin\Release\TournamentAssistant.dll"),
    (Join-Path $pluginDir "bin\Release\net472\TournamentAssistant.dll"),
    (Join-Path $pluginDir "bin\Release\net48\TournamentAssistant.dll"),
    (Join-Path $pluginDir "bin\Release\net462\TournamentAssistant.dll")
)

# --- Helpers -----------------------------------------------------------------

function Write-Section {
    param([string]$Message)
    Write-Host ""
    Write-Host "==== $Message ====" -ForegroundColor Cyan
}

function Require-Command {
    param([string]$CommandName)
    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $CommandName"
    }
}

function Run-Step {
    param(
        [string]$WorkingDirectory,
        [string]$FilePath,
        [string[]]$Arguments = @()
    )

    Write-Host "[$WorkingDirectory] $FilePath $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Clear-Directory {
    param([string]$Path)
    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Set-BeatSaberDir {
    param(
        [string]$UserFilePath,
        [string]$BeatSaberDir
    )

    if (-not (Test-Path $UserFilePath)) {
        throw "Could not find $UserFilePath"
    }

    $content = Get-Content $UserFilePath -Raw

    if ($content -match '<BeatSaberDir>.*?</BeatSaberDir>') {
        $escapedDir = [System.Security.SecurityElement]::Escape($BeatSaberDir)
        $content = [regex]::Replace(
            $content,
            '<BeatSaberDir>.*?</BeatSaberDir>',
            "<BeatSaberDir>$escapedDir</BeatSaberDir>",
            [System.Text.RegularExpressions.RegexOptions]::Singleline
        )
    }
    else {
        $escapedDir = [System.Security.SecurityElement]::Escape($BeatSaberDir)

        if ($content -match '</PropertyGroup>') {
            $content = [regex]::Replace(
                $content,
                '</PropertyGroup>',
                "  <BeatSaberDir>$escapedDir</BeatSaberDir>`r`n</PropertyGroup>",
                [System.Text.RegularExpressions.RegexOptions]::Singleline,
                1
            )
        }
        else {
            $content = @"
<Project>
  <PropertyGroup>
    <BeatSaberDir>$escapedDir</BeatSaberDir>
  </PropertyGroup>
</Project>
"@
        }
    }

    Set-Content -Path $UserFilePath -Value $content -Encoding UTF8
    Write-Host "Updated BeatSaberDir -> $BeatSaberDir" -ForegroundColor Yellow
}

function Resolve-PluginOutput {
    param([string[]]$Candidates)

    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $binDir = Join-Path $pluginDir "bin"
    if (Test-Path $binDir) {
        $dll = Get-ChildItem -Path $binDir -Recurse -File -Filter "TournamentAssistant.dll" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

        if ($dll) {
            return $dll.FullName
        }
    }

    throw "Could not find built TournamentAssistant.dll under $binDir"
}

function Get-CurrentBranch {
    Push-Location $repoRoot
    try {
        $branch = (& git branch --show-current).Trim()
        if (-not $branch) {
            throw "Failed to determine current branch."
        }
        return $branch
    }
    finally {
        Pop-Location
    }
}

function Copy-DirectoryContents {
    param(
        [string]$SourceDir,
        [string]$DestinationDir
    )

    Ensure-Directory $DestinationDir
    Copy-Item (Join-Path $SourceDir "*") $DestinationDir -Recurse -Force
}

# --- Validation ---------------------------------------------------------------

Write-Section "Validating prerequisites"

Require-Command "git"
Require-Command "npm"
Require-Command "dotnet"

if (-not (Test-Path $uiDir)) { throw "Missing UI directory: $uiDir" }
if (-not (Test-Path $pluginProject)) { throw "Missing plugin project: $pluginProject" }
if (-not (Test-Path $pluginUserFile)) { throw "Missing plugin user file: $pluginUserFile" }

Push-Location $repoRoot
try {
    $status = (& git status --porcelain)
    if ($status) {
        throw "Working tree is not clean. Please commit/stash changes before running this script."
    }

    & git fetch --all --prune
    if ($LASTEXITCODE -ne 0) {
        throw "git fetch failed."
    }
}
finally {
    Pop-Location
}

$currentBranch = Get-CurrentBranch
Write-Host "Starting branch: $currentBranch"

Clear-Directory $artifactsRoot

# --- Build UI once from master ------------------------------------------------

if (-not $SkipUi) {
    Write-Section "Building UI from master"

    Push-Location $repoRoot
    try {
        & git checkout master
        if ($LASTEXITCODE -ne 0) { throw "Failed to checkout master" }
    }
    finally {
        Pop-Location
    }

    $uiArtifactsDir = Join-Path $artifactsRoot "UI"
    $uiWebArtifactsDir = Join-Path $uiArtifactsDir "Website"
    $uiDesktopArtifactsDir = Join-Path $uiArtifactsDir "Desktop"

    Ensure-Directory $uiArtifactsDir
    Ensure-Directory $uiWebArtifactsDir
    Ensure-Directory $uiDesktopArtifactsDir

    Run-Step -WorkingDirectory $uiDir -FilePath "powershell" -Arguments @("-ExecutionPolicy", "Bypass", "-File", ".\build_protos.ps1")
    Run-Step -WorkingDirectory $uiDir -FilePath "npm" -Arguments @("install")
    Run-Step -WorkingDirectory $uiDir -FilePath "npm" -Arguments @("run", "build")

    if (-not (Test-Path $uiWebBuildDir)) {
        throw "UI web build output not found: $uiWebBuildDir"
    }

    Copy-DirectoryContents -SourceDir $uiWebBuildDir -DestinationDir $uiWebArtifactsDir
    Write-Host "Copied website build -> $uiWebArtifactsDir" -ForegroundColor Green

    Run-Step -WorkingDirectory $uiDir -FilePath "npm" -Arguments @("run", "tauri", "build")

    if (-not (Test-Path $tauriExePath)) {
        throw "Tauri exe not found: $tauriExePath"
    }

    Copy-Item $tauriExePath (Join-Path $uiDesktopArtifactsDir "taui.exe") -Force
    Write-Host "Copied desktop build -> $uiDesktopArtifactsDir\taui.exe" -ForegroundColor Green
}

# --- Build plugin for each game version --------------------------------------

Write-Section "Building plugin for all versions"

foreach ($entry in $branches) {
    $branch = $entry.Branch
    $version = $entry.Version
    $beatSaberDir = Join-Path $beatSaberBaseDir $version
    $pluginArtifactsDir = Join-Path $artifactsRoot "Plugin"

    Ensure-Directory $pluginArtifactsDir

    Write-Section "Branch: $branch | Version: $version"

    Push-Location $repoRoot
    try {
        & git checkout $branch
        if ($LASTEXITCODE -ne 0) { throw "Failed to checkout $branch" }

        if (($branch -ne "master") -and (-not $NoMerge)) {
            & git merge --no-edit master
            if ($LASTEXITCODE -ne 0) {
                throw "git merge master failed on branch $branch"
            }
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path $beatSaberDir)) {
        throw "Beat Saber directory does not exist for $version : $beatSaberDir"
    }

    Set-BeatSaberDir -UserFilePath $pluginUserFile -BeatSaberDir $beatSaberDir

    # Clean old outputs so we don't accidentally grab a stale dll
    $pluginBinDir = Join-Path $pluginDir "bin"
    if (Test-Path $pluginBinDir) {
        Remove-Item $pluginBinDir -Recurse -Force
    }

    Run-Step -WorkingDirectory $repoRoot -FilePath "dotnet" -Arguments @("build", $pluginProject, "-c", "Release")

    $builtDll = Resolve-PluginOutput -Candidates $pluginOutputCandidates
    $artifactDllName = "TournamentAssistant_$version.dll"
    $artifactDllPath = Join-Path $pluginArtifactsDir $artifactDllName

    Copy-Item $builtDll $artifactDllPath -Force
    Write-Host "Copied plugin -> $artifactDllPath" -ForegroundColor Green
}

# --- Restore original branch --------------------------------------------------

Write-Section "Restoring original branch"

Push-Location $repoRoot
try {
    & git checkout $currentBranch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore original branch: $currentBranch"
    }
}
finally {
    Pop-Location
}

Write-Section "Done"
Write-Host "Artifacts written to: $artifactsRoot" -ForegroundColor Greenparam(
    [string]$RepoRoot = ".",
    [switch]$SkipUi,
    [switch]$NoMerge
)

$ErrorActionPreference = "Stop"

# --- Configuration ------------------------------------------------------------

$branches = @(
    @{ Branch = "master"; Version = "1.29.1" },
    @{ Branch = "ssl-tauri-discord-1.34.2"; Version = "1.34.2" },
    @{ Branch = "ssl-tauri-discord-1.37.0"; Version = "1.37.0" },
    @{ Branch = "ssl-tauri-discord-1.39.1"; Version = "1.39.1" },
    @{ Branch = "ssl-tauri-discord-1.40.8"; Version = "1.40.8" },
    @{ Branch = "ssl-tauri-discord-1.41.1"; Version = "1.41.1" },
    @{ Branch = "ssl-tauri-discord-1.42.0"; Version = "1.42.0" }
)

$repoRoot = (Resolve-Path $RepoRoot).Path
$uiDir = Join-Path $repoRoot "TournamentAssistantUI"
$pluginDir = Join-Path $repoRoot "TournamentAssistant"
$pluginProject = Join-Path $pluginDir "TournamentAssistant.csproj"
$pluginUserFile = Join-Path $pluginDir "TournamentAssistant.csproj.user"
$artifactsRoot = Join-Path $repoRoot "Artifacts"

# Adjust if your Beat Saber installations live somewhere else
$beatSaberBaseDir = "O:\BSManager\BSInstances"

# Tauri output locations
$uiWebBuildDir = Join-Path $uiDir "build"
$tauriExePath = Join-Path $uiDir "src-tauri\target\release\taui.exe"

# Plugin output guesses. We'll probe these in order.
$pluginOutputCandidates = @(
    (Join-Path $pluginDir "bin\Release\TournamentAssistant.dll"),
    (Join-Path $pluginDir "bin\Release\net472\TournamentAssistant.dll"),
    (Join-Path $pluginDir "bin\Release\net48\TournamentAssistant.dll"),
    (Join-Path $pluginDir "bin\Release\net462\TournamentAssistant.dll")
)

# --- Helpers -----------------------------------------------------------------

function Write-Section {
    param([string]$Message)
    Write-Host ""
    Write-Host "==== $Message ====" -ForegroundColor Cyan
}

function Require-Command {
    param([string]$CommandName)
    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $CommandName"
    }
}

function Run-Step {
    param(
        [string]$WorkingDirectory,
        [string]$FilePath,
        [string[]]$Arguments = @()
    )

    Write-Host "[$WorkingDirectory] $FilePath $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Clear-Directory {
    param([string]$Path)
    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Set-BeatSaberDir {
    param(
        [string]$UserFilePath,
        [string]$BeatSaberDir
    )

    if (-not (Test-Path $UserFilePath)) {
        throw "Could not find $UserFilePath"
    }

    $content = Get-Content $UserFilePath -Raw

    if ($content -match '<BeatSaberDir>.*?</BeatSaberDir>') {
        $escapedDir = [System.Security.SecurityElement]::Escape($BeatSaberDir)
        $content = [regex]::Replace(
            $content,
            '<BeatSaberDir>.*?</BeatSaberDir>',
            "<BeatSaberDir>$escapedDir</BeatSaberDir>",
            [System.Text.RegularExpressions.RegexOptions]::Singleline
        )
    }
    else {
        $escapedDir = [System.Security.SecurityElement]::Escape($BeatSaberDir)

        if ($content -match '</PropertyGroup>') {
            $content = [regex]::Replace(
                $content,
                '</PropertyGroup>',
                "  <BeatSaberDir>$escapedDir</BeatSaberDir>`r`n</PropertyGroup>",
                [System.Text.RegularExpressions.RegexOptions]::Singleline,
                1
            )
        }
        else {
            $content = @"
<Project>
  <PropertyGroup>
    <BeatSaberDir>$escapedDir</BeatSaberDir>
  </PropertyGroup>
</Project>
"@
        }
    }

    Set-Content -Path $UserFilePath -Value $content -Encoding UTF8
    Write-Host "Updated BeatSaberDir -> $BeatSaberDir" -ForegroundColor Yellow
}

function Resolve-PluginOutput {
    param([string[]]$Candidates)

    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $binDir = Join-Path $pluginDir "bin"
    if (Test-Path $binDir) {
        $dll = Get-ChildItem -Path $binDir -Recurse -File -Filter "TournamentAssistant.dll" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

        if ($dll) {
            return $dll.FullName
        }
    }

    throw "Could not find built TournamentAssistant.dll under $binDir"
}

function Get-CurrentBranch {
    Push-Location $repoRoot
    try {
        $branch = (& git branch --show-current).Trim()
        if (-not $branch) {
            throw "Failed to determine current branch."
        }
        return $branch
    }
    finally {
        Pop-Location
    }
}

function Copy-DirectoryContents {
    param(
        [string]$SourceDir,
        [string]$DestinationDir
    )

    Ensure-Directory $DestinationDir
    Copy-Item (Join-Path $SourceDir "*") $DestinationDir -Recurse -Force
}

# --- Validation ---------------------------------------------------------------

Write-Section "Validating prerequisites"

Require-Command "git"
Require-Command "npm"
Require-Command "dotnet"

if (-not (Test-Path $uiDir)) { throw "Missing UI directory: $uiDir" }
if (-not (Test-Path $pluginProject)) { throw "Missing plugin project: $pluginProject" }
if (-not (Test-Path $pluginUserFile)) { throw "Missing plugin user file: $pluginUserFile" }

Push-Location $repoRoot
try {
    $status = (& git status --porcelain)
    if ($status) {
        throw "Working tree is not clean. Please commit/stash changes before running this script."
    }

    & git fetch --all --prune
    if ($LASTEXITCODE -ne 0) {
        throw "git fetch failed."
    }
}
finally {
    Pop-Location
}

$currentBranch = Get-CurrentBranch
Write-Host "Starting branch: $currentBranch"

Clear-Directory $artifactsRoot

# --- Build UI once from master ------------------------------------------------

if (-not $SkipUi) {
    Write-Section "Building UI from master"

    Push-Location $repoRoot
    try {
        & git checkout master
        if ($LASTEXITCODE -ne 0) { throw "Failed to checkout master" }
    }
    finally {
        Pop-Location
    }

    $uiArtifactsDir = Join-Path $artifactsRoot "UI"
    $uiWebArtifactsDir = Join-Path $uiArtifactsDir "Website"
    $uiDesktopArtifactsDir = Join-Path $uiArtifactsDir "Desktop"

    Ensure-Directory $uiArtifactsDir
    Ensure-Directory $uiWebArtifactsDir
    Ensure-Directory $uiDesktopArtifactsDir

    Run-Step -WorkingDirectory $uiDir -FilePath "powershell" -Arguments @("-ExecutionPolicy", "Bypass", "-File", ".\build_protos.ps1")
    Run-Step -WorkingDirectory $uiDir -FilePath "npm" -Arguments @("install")
    Run-Step -WorkingDirectory $uiDir -FilePath "npm" -Arguments @("run", "build")

    if (-not (Test-Path $uiWebBuildDir)) {
        throw "UI web build output not found: $uiWebBuildDir"
    }

    Copy-DirectoryContents -SourceDir $uiWebBuildDir -DestinationDir $uiWebArtifactsDir
    Write-Host "Copied website build -> $uiWebArtifactsDir" -ForegroundColor Green

    Run-Step -WorkingDirectory $uiDir -FilePath "npm" -Arguments @("run", "tauri", "build")

    if (-not (Test-Path $tauriExePath)) {
        throw "Tauri exe not found: $tauriExePath"
    }

    Copy-Item $tauriExePath (Join-Path $uiDesktopArtifactsDir "taui.exe") -Force
    Write-Host "Copied desktop build -> $uiDesktopArtifactsDir\taui.exe" -ForegroundColor Green
}

# --- Build plugin for each game version --------------------------------------

Write-Section "Building plugin for all versions"

foreach ($entry in $branches) {
    $branch = $entry.Branch
    $version = $entry.Version
    $beatSaberDir = Join-Path $beatSaberBaseDir $version
    $pluginArtifactsDir = Join-Path $artifactsRoot "Plugin"

    Ensure-Directory $pluginArtifactsDir

    Write-Section "Branch: $branch | Version: $version"

    Push-Location $repoRoot
    try {
        & git checkout $branch
        if ($LASTEXITCODE -ne 0) { throw "Failed to checkout $branch" }

        if (($branch -ne "master") -and (-not $NoMerge)) {
            & git merge --no-edit master
            if ($LASTEXITCODE -ne 0) {
                throw "git merge master failed on branch $branch"
            }
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path $beatSaberDir)) {
        throw "Beat Saber directory does not exist for $version : $beatSaberDir"
    }

    Set-BeatSaberDir -UserFilePath $pluginUserFile -BeatSaberDir $beatSaberDir

    # Clean old outputs so we don't accidentally grab a stale dll
    $pluginBinDir = Join-Path $pluginDir "bin"
    if (Test-Path $pluginBinDir) {
        Remove-Item $pluginBinDir -Recurse -Force
    }

    Run-Step -WorkingDirectory $repoRoot -FilePath "dotnet" -Arguments @("build", $pluginProject, "-c", "Release")

    $builtDll = Resolve-PluginOutput -Candidates $pluginOutputCandidates
    $artifactDllName = "TournamentAssistant_$version.dll"
    $artifactDllPath = Join-Path $pluginArtifactsDir $artifactDllName

    Copy-Item $builtDll $artifactDllPath -Force
    Write-Host "Copied plugin -> $artifactDllPath" -ForegroundColor Green
}

# --- Restore original branch --------------------------------------------------

Write-Section "Restoring original branch"

Push-Location $repoRoot
try {
    & git checkout $currentBranch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore original branch: $currentBranch"
    }
}
finally {
    Pop-Location
}

Write-Section "Done"
Write-Host "Artifacts written to: $artifactsRoot" -ForegroundColor Green