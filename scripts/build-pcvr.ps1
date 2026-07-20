param(
    [string]$RepoRoot = "..",
    [ValidateSet("All", "1.29.1", "1.34.2", "1.39.1", "1.40.8", "1.41.1", "1.42.0")]
    [string]$GameVersion = "All",
    [string]$PluginVersion = "1.3.0",
    [string]$ReferencesRoot,
    [string]$BeatSaberBaseDir = "O:\BSManager\BSInstances",
    [switch]$NoBuild,
    [switch]$KeepStage
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot $RepoRoot)).Path
$stageBase = Join-Path $repoRoot ".build/pcvr"
$artifactBase = Join-Path $repoRoot "Artifacts/$PluginVersion/PCVR"
$compatibilityBase = "fe54031472ffe947be442c947ab9f8c0fd08bfda"

# The compatibility refs are immutable inputs. New work lives only in the
# current branch; this script never checks out, merges, or edits these refs.
$targets = @(
    [pscustomobject]@{ Version = "1.29.1"; Ref = $null },
    [pscustomobject]@{ Version = "1.34.2"; Ref = "eb13a51ebdac0703dc77995edf75a8242a37d030" },
    [pscustomobject]@{ Version = "1.39.1"; Ref = "f67b16ce9cac10c89965e711849658ee6a1b4d19" },
    [pscustomobject]@{ Version = "1.40.8"; Ref = "461fc0c2de198dc68d93fe80d3af5919d3cb4784" },
    [pscustomobject]@{ Version = "1.41.1"; Ref = "5ba88060117bc6646d6f8b076d546f0c4076ff40" },
    [pscustomobject]@{ Version = "1.42.0"; Ref = "444319b0a3f178533e191d622c8b05ae201ad7ad" }
)

function Write-Section([string]$Message) {
    Write-Host ""
    Write-Host "==== $Message ====" -ForegroundColor Cyan
}

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command was not found: $Name"
    }
}

function Invoke-Native([string]$WorkingDirectory, [string]$Command, [string[]]$Arguments) {
    Write-Host "[$WorkingDirectory] $Command $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    try {
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
    }
    finally { Pop-Location }
}

function Copy-SourceTree([string]$Source, [string]$Destination) {
    if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem $Source -Force |
        Where-Object { $_.Name -notin @("bin", "obj") } |
        Copy-Item -Destination $Destination -Recurse -Force
}

function Export-GitFile([string]$Ref, [string]$RelativePath, [string]$Destination) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "git"
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add("show")
    $startInfo.ArgumentList.Add("$Ref`:$RelativePath")
    $process = [Diagnostics.Process]::Start($startInfo)
    $stream = [IO.File]::Create($Destination)
    try { $process.StandardOutput.BaseStream.CopyTo($stream) }
    finally { $stream.Dispose() }
    $errorText = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "git show failed for $Ref`:$RelativePath`n$errorText" }
}

function Set-Text([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Replace-Required([string]$Path, [string]$Pattern, [string]$Replacement) {
    $text = [IO.File]::ReadAllText($Path)
    if (-not [regex]::IsMatch($text, $Pattern)) {
        throw "Compatibility injection pattern was not found in $Path : $Pattern"
    }
    Set-Text $Path ([regex]::Replace($text, $Pattern, $Replacement, 1))
}

function Add-ReplayStreaming([string]$StageRoot, [string]$TargetVersion) {
    $project = Join-Path $StageRoot "TournamentAssistant/TournamentAssistant.csproj"
    $projectText = [IO.File]::ReadAllText($project)
    if ($projectText -notmatch 'Behaviors\\ReplayStreamer\.cs') {
        $projectText = $projectText.Replace(
            '<Compile Include="Behaviors\ScoreMonitor.cs" />',
            "<Compile Include=`"Behaviors\ScoreMonitor.cs`" />`r`n    <Compile Include=`"Behaviors\ReplayStreamer.cs`" />"
        )
    }
    $projectText = $projectText.Replace(
        '<Target Name="CopyOutputToDestinationFolder" AfterTargets="ILRepack">',
        '<Target Name="CopyOutputToDestinationFolder" AfterTargets="ILRepack" Condition="''$(DisableCopyToGame)'' != ''true''">'
    )
    Set-Text $project $projectText

    $manifestPath = Join-Path $StageRoot "TournamentAssistant/manifest.json"
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.gameVersion = $TargetVersion
    $manifest.version = $PluginVersion
    Set-Text $manifestPath ($manifest | ConvertTo-Json -Depth 30)

    $qualifier = Join-Path $StageRoot "TournamentAssistant/UI/FlowCoordinators/QualifierCoordinator.cs"
    $qualifierText = [IO.File]::ReadAllText($qualifier)
    if ($qualifierText -notmatch 'ReplayStreamer\.GameplayParameters') {
        Replace-Required $qualifier '(PrePlaySetup\(\);\s+)(SongUtils\.PlaySong\()' "`$1ReplayStreamer.GameplayParameters = _currentMap.GameplayParameters;`r`n            `$2"
    }
    $qualifierText = [IO.File]::ReadAllText($qualifier)
    if ($qualifierText -notmatch 'ReplayStreamer\.Complete\(results\)') {
        Replace-Required $qualifier '(standardLevelScenesTransitionSetupData\.didFinishEvent -= SongFinished;)' "`$1`r`n            ReplayStreamer.Complete(results);"
    }

    $room = Join-Path $StageRoot "TournamentAssistant/UI/FlowCoordinators/RoomCoordinator.cs"
    $roomText = [IO.File]::ReadAllText($room)
    if ($roomText -notmatch 'ReplayStreamer\.Complete\(results\)') {
        Replace-Required $room '(standardLevelScenesTransitionSetupData\.didFinishEvent -= SongFinished;)' "`$1`r`n            ReplayStreamer.Complete(results);"
    }
}

function Resolve-References([string]$TargetVersion) {
    $candidates = @()
    if ($ReferencesRoot) { $candidates += Join-Path $ReferencesRoot $TargetVersion }
    if ($BeatSaberBaseDir) { $candidates += Join-Path $BeatSaberBaseDir $TargetVersion }
    foreach ($candidate in $candidates) {
        if ((Test-Path (Join-Path $candidate "Beat Saber_Data/Managed")) -and (Test-Path (Join-Path $candidate "Plugins"))) {
            return (Resolve-Path $candidate).Path
        }
    }
    throw "No complete references found for Beat Saber $TargetVersion. Checked: $($candidates -join ', ')"
}

function New-PackagesLink([string]$StageRoot) {
    $source = Join-Path $repoRoot "packages"
    if (-not (Test-Path $source)) { throw "NuGet packages directory is missing: $source" }
    $destination = Join-Path $StageRoot "packages"
    if ($IsWindows) { New-Item -ItemType Junction -Path $destination -Target $source | Out-Null }
    else { New-Item -ItemType SymbolicLink -Path $destination -Target $source | Out-Null }
}

Require-Command "git"
if (-not $NoBuild) { Require-Command "dotnet" }
if ($PluginVersion -notmatch '^\d+\.\d+\.\d+$') { throw "PluginVersion must be major.minor.patch" }

if ($GameVersion -ne "All") {
    $targets = @($targets | Where-Object Version -eq $GameVersion)
}
New-Item -ItemType Directory -Force -Path $stageBase, $artifactBase | Out-Null

foreach ($target in $targets) {
    Write-Section "Preparing PCVR $($target.Version) from the current branch"
    $stageRoot = Join-Path $stageBase $target.Version
    if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $stageRoot | Out-Null

    Copy-SourceTree (Join-Path $repoRoot "TournamentAssistant") (Join-Path $stageRoot "TournamentAssistant")
    Copy-SourceTree (Join-Path $repoRoot "TournamentAssistantShared") (Join-Path $stageRoot "TournamentAssistantShared")
    $stagedProtos = Join-Path $stageRoot "TournamentAssistantProtos"
    New-Item -ItemType Directory -Path $stagedProtos | Out-Null
    Get-ChildItem (Join-Path $repoRoot "TournamentAssistantProtos") -File -Filter "*.proto" |
        Copy-Item -Destination $stagedProtos -Force
    New-PackagesLink $stageRoot

    if ($target.Ref) {
        Invoke-Native $repoRoot "git" @("rev-parse", "--verify", "$($target.Ref)^{commit}")
        $patchPath = Join-Path ([IO.Path]::GetTempPath()) "ta-pcvr-$($target.Version)-$PID.patch"
        try {
            Invoke-Native $repoRoot "git" @(
                "diff", "--binary", "--output=$patchPath", $compatibilityBase, $target.Ref, "--", "TournamentAssistant",
                ":(exclude)TournamentAssistant/TournamentAssistant.csproj",
                ":(exclude)TournamentAssistant/manifest.json",
                ":(exclude)TournamentAssistant/UI/FlowCoordinators/QualifierCoordinator.cs",
                ":(exclude)TournamentAssistant/UI/FlowCoordinators/RoomCoordinator.cs"
            )
            $stageRelative = [IO.Path]::GetRelativePath($repoRoot, $stageRoot).Replace('\', '/')
            Invoke-Native $repoRoot "git" @("apply", "--directory=$stageRelative", "--whitespace=nowarn", $patchPath)
        }
        finally { if (Test-Path $patchPath) { Remove-Item $patchPath -Force } }

        foreach ($file in @(
            "TournamentAssistant/TournamentAssistant.csproj",
            "TournamentAssistant/manifest.json",
            "TournamentAssistant/UI/FlowCoordinators/QualifierCoordinator.cs",
            "TournamentAssistant/UI/FlowCoordinators/RoomCoordinator.cs"
        )) {
            Export-GitFile $target.Ref $file (Join-Path $stageRoot $file)
        }
    }

    Add-ReplayStreaming $stageRoot $target.Version
    if ($NoBuild) {
        Write-Host "Prepared source only: $stageRoot" -ForegroundColor Yellow
        continue
    }

    $refs = Resolve-References $target.Version
    $project = Join-Path $stageRoot "TournamentAssistant/TournamentAssistant.csproj"
    Invoke-Native $stageRoot "dotnet" @(
        "build", $project, "-c", "Release",
        "-p:BeatSaberDir=$refs",
        "-p:DisableCopyToGame=true"
    )
    $dll = Join-Path $stageRoot "TournamentAssistant/bin/Release/TournamentAssistant.dll"
    if (-not (Test-Path $dll)) { throw "PCVR build output is missing: $dll" }
    $artifact = Join-Path $artifactBase "TournamentAssistant-$PluginVersion-bs$($target.Version).dll"
    Copy-Item $dll $artifact -Force
    Write-Host "Artifact: $artifact" -ForegroundColor Green

    if (-not $KeepStage) { Remove-Item $stageRoot -Recurse -Force }
}

Write-Section "PCVR build complete"
Write-Host "Artifacts: $artifactBase" -ForegroundColor Green
