param(
    [string]$RepoRoot = "..",
    [string]$ServerUrl,
    [string]$Version,
    [ValidateSet("Prompt", "Generate", "Import", "Keep")]
    [string]$CertificateMode = "Prompt",
    [string]$CertificateImportPath,
    [ValidateSet("Prompt", "DirectTls", "ReverseProxy")]
    [string]$DeploymentMode = "Prompt",
    [int]$RawPort = 8675,
    [int]$WebsocketPort = 8676,
    [int]$ApiPort = 8678,
    [switch]$NoRestore,
    [switch]$NoDockerBuild,
    [switch]$NativeServerPublish,
    [switch]$SkipStandalone,
    [switch]$SkipClient,
    [switch]$SkipUi,
    [switch]$SkipPcvr,
    [switch]$SkipCertificates,
    [ValidateSet("All", "1.29.1", "1.34.2", "1.39.1", "1.40.8", "1.41.1", "1.42.0")]
    [string]$PcvrGameVersion = "All",
    [string]$PcvrReferencesRoot,
    [string]$BeatSaberBaseDir = "O:\BSManager\BSInstances"
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot $RepoRoot)).Path
$deploymentFiles = Join-Path $repoRoot "deployment/files"
$artifactsRoot = Join-Path $repoRoot "Artifacts"

function Write-Section([string]$Message) {
    Write-Host ""
    Write-Host "==== $Message ====" -ForegroundColor Cyan
}

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command was not found: $Name"
    }
}

function Invoke-Step([string]$WorkingDirectory, [string]$Command, [string[]]$Arguments = @()) {
    Write-Host "[$WorkingDirectory] $Command $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    try {
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
    }
    finally { Pop-Location }
}

function Read-Required([string]$Prompt, [string]$Current = "") {
    if ($Current) { return $Current }
    do { $value = (Read-Host $Prompt).Trim() } while (-not $value)
    return $value
}

function Read-Choice([string]$Prompt, [string[]]$Allowed, [string]$Default) {
    do {
        $value = (Read-Host "$Prompt [$Default]").Trim()
        if (-not $value) { $value = $Default }
    } while ($Allowed -notcontains $value)
    return $value
}

function Set-Regex([string]$RelativePath, [string]$Pattern, [string]$Replacement) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path $path)) { throw "Version/address target does not exist: $path" }
    $text = [IO.File]::ReadAllText($path)
    $updated = [regex]::Replace($text, $Pattern, $Replacement)
    if ($updated -eq $text -and -not [regex]::IsMatch($text, $Pattern)) {
        throw "Pattern was not found in $RelativePath : $Pattern"
    }
    [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false))
}

function Set-JsonVersion([string]$RelativePath, [string[]]$PropertyPath, [string]$Value) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path $path)) { return }
    $json = Get-Content $path -Raw | ConvertFrom-Json
    $target = $json
    for ($index = 0; $index -lt $PropertyPath.Count - 1; ++$index) {
        $target = $target.($PropertyPath[$index])
    }
    $target.($PropertyPath[-1]) = $Value
    $json | ConvertTo-Json -Depth 100 | Set-Content $path -Encoding utf8
}

function Set-ObjectProperty($Object, [string]$Name, $Value) {
    $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force
}

function Test-Pfx([string]$Path, [string]$Password) {
    & openssl pkcs12 -in $Path -passin "pass:$Password" -info -noout -legacy 2>$null
    if ($LASTEXITCODE -ne 0) {
        & openssl pkcs12 -in $Path -passin "pass:$Password" -info -noout 2>$null
    }
    if ($LASTEXITCODE -ne 0) { throw "PFX validation failed: $Path" }
}

function Test-Qmod([string]$Path, [string]$ExpectedVersion, [string]$BuiltLibrary) {
    if (-not (Test-Path $Path)) { throw "QMOD was not produced: $Path" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $manifestEntry = $archive.GetEntry("mod.json")
        $libraryEntry = $archive.GetEntry("libTAStandalone.so")
        if (-not $manifestEntry -or -not $libraryEntry) { throw "QMOD is missing mod.json or libTAStandalone.so" }
        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json }
        finally { $reader.Dispose() }
        if ($manifest.version -ne $ExpectedVersion) {
            throw "QMOD version is '$($manifest.version)', expected '$ExpectedVersion'"
        }
        if ((Test-Path $BuiltLibrary) -and $libraryEntry.Length -ne (Get-Item $BuiltLibrary).Length) {
            throw "QMOD contains a stale standalone library (packaged and built sizes differ)"
        }
    }
    finally { $archive.Dispose() }
}

function New-Pfx([string]$Name, [string]$CommonName, [string]$Password, [string]$San = "") {
    $key = Join-Path $deploymentFiles "$Name.key"
    $crt = Join-Path $deploymentFiles "$Name.crt"
    $pfx = Join-Path $deploymentFiles "$Name.pfx"
    $arguments = @("req", "-x509", "-newkey", "rsa:3072", "-sha256", "-days", "825", "-nodes", "-keyout", $key, "-out", $crt, "-subj", "/CN=$CommonName")
    if ($San) { $arguments += @("-addext", "subjectAltName=$San") }
    Invoke-Step $repoRoot "openssl" $arguments
    Invoke-Step $repoRoot "openssl" @(
        "pkcs12", "-export", "-out", $pfx, "-inkey", $key, "-in", $crt,
        "-name", $CommonName, "-keypbe", "PBE-SHA1-3DES", "-certpbe",
        "PBE-SHA1-3DES", "-macalg", "sha1", "-passout", "pass:$Password"
    )
    Test-Pfx $pfx $Password
}

function Configure-Certificates([string]$Mode, [string]$ImportPath, [string]$HostName) {
    New-Item -ItemType Directory -Force -Path $deploymentFiles | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $deploymentFiles "FileServerContent") | Out-Null

    if ($Mode -eq "Prompt") {
        $answer = Read-Choice "Certificates: generate, import, or keep existing?" @("generate", "import", "keep") "generate"
        $Mode = switch ($answer) { "generate" { "Generate" } "import" { "Import" } default { "Keep" } }
    }

    if ($Mode -eq "Import") {
        $ImportPath = Read-Required "Directory containing server.pfx, player.pfx, mock.pfx, and beatkhana-public.pem" $ImportPath
        $ImportPath = (Resolve-Path $ImportPath).Path
        foreach ($file in @("server.pfx", "player.pfx", "mock.pfx", "beatkhana-public.pem")) {
            $source = Join-Path $ImportPath $file
            if (-not (Test-Path $source)) { throw "Certificate import is missing $source" }
            $destination = Join-Path $deploymentFiles $file
            $sourceFull = [IO.Path]::GetFullPath($source)
            $destinationFull = [IO.Path]::GetFullPath($destination)
            if (-not $sourceFull.Equals($destinationFull, [StringComparison]::OrdinalIgnoreCase)) {
                Copy-Item $source $destination -Force
            }
        }
    }
    elseif ($Mode -eq "Generate") {
        $ip = $null
        $isIp = [Net.IPAddress]::TryParse($HostName, [ref]$ip)
        $san = if ($isIp) { "IP:$HostName,IP:127.0.0.1,DNS:localhost" } else { "DNS:$HostName,DNS:localhost,IP:127.0.0.1" }
        New-Pfx "server" $HostName "password" $san
        New-Pfx "player" "TournamentAssistant Player Token" "TAPlayerPass"
        New-Pfx "mock" "TournamentAssistant Mock Token" "password"
        Invoke-Step $repoRoot "openssl" @("genpkey", "-algorithm", "RSA", "-pkeyopt", "rsa_keygen_bits:3072", "-out", (Join-Path $deploymentFiles "beatkhana-private.pem"))
        Invoke-Step $repoRoot "openssl" @("pkey", "-in", (Join-Path $deploymentFiles "beatkhana-private.pem"), "-pubout", "-out", (Join-Path $deploymentFiles "beatkhana-public.pem"))
        Write-Warning "The generated BeatKhana key is for startup/local development only. Import BeatKhana's real public key before accepting real BeatKhana game tokens."
    }

    foreach ($file in @("server.pfx", "player.pfx", "mock.pfx", "beatkhana-public.pem")) {
        if (-not (Test-Path (Join-Path $deploymentFiles $file))) { throw "Required certificate/key is missing: $file" }
    }
    Test-Pfx (Join-Path $deploymentFiles "server.pfx") "password"
    Test-Pfx (Join-Path $deploymentFiles "player.pfx") "TAPlayerPass"
    Test-Pfx (Join-Path $deploymentFiles "mock.pfx") "password"
}

function Write-ServerConfig([string]$HostName, [string]$Mode, [int]$PublicWebsocketPort) {
    $path = Join-Path $deploymentFiles "serverConfig.json"
    if (Test-Path $path) { $config = Get-Content $path -Raw | ConvertFrom-Json }
    else { $config = [pscustomobject]@{} }
    $direct = $Mode -eq "DirectTls"
    # These are the fixed ports inside the container. Public host ports are
    # configured independently in .env and stamped into the clients below.
    Set-ObjectProperty $config "port" "8675"
    Set-ObjectProperty $config "serverName" "TournamentAssistant Server"
    Set-ObjectProperty $config "serverAddress" $HostName
    Set-ObjectProperty $config "overlayPort" "8676"
    Set-ObjectProperty $config "websocketUseSsl" $(if ($direct) { "True" } else { "False" })
    Set-ObjectProperty $config "apiUseSsl" $(if ($direct) { "True" } else { "False" })
    Set-ObjectProperty $config "oauthPort" "0"
    if (-not $config.PSObject.Properties["discordClientId"]) { Set-ObjectProperty $config "discordClientId" "[discordClientId]" }
    if (-not $config.PSObject.Properties["discordClientSecret"]) { Set-ObjectProperty $config "discordClientSecret" "[discordClientSecret]" }
    if (-not $config.PSObject.Properties["botToken"]) { Set-ObjectProperty $config "botToken" "[botToken]" }
    Set-ObjectProperty $config "servers" @([pscustomobject]@{
        address = $HostName
        port = "$RawPort"
        websocketPort = "$PublicWebsocketPort"
        name = "TournamentAssistant Server"
    })
    $config | ConvertTo-Json -Depth 20 | Set-Content $path -Encoding utf8
}

function Stamp-Sources([string]$HostName, [string]$ReleaseVersion, [int]$VersionCode, [int]$PublicWebsocketPort, [int]$PublicApiPort) {
    Write-Section "Stamping version $ReleaseVersion and server $HostName"
    Set-Regex "TournamentAssistantShared/Constants.cs" 'PLUGIN_VERSION = "[^"]+"' "PLUGIN_VERSION = `"$ReleaseVersion`""
    Set-Regex "TournamentAssistantShared/Constants.cs" 'PLUGIN_VERSION_CODE = \d+' "PLUGIN_VERSION_CODE = $VersionCode"
    Set-Regex "TournamentAssistantShared/Constants.cs" 'WEBSOCKET_VERSION = "[^"]+"' "WEBSOCKET_VERSION = `"$ReleaseVersion`""
    Set-Regex "TournamentAssistantShared/Constants.cs" 'WEBSOCKET_VERSION_CODE = \d+' "WEBSOCKET_VERSION_CODE = $VersionCode"
    Set-Regex "TournamentAssistantShared/Constants.cs" 'SERVER_VERSION = "[^"]+"' "SERVER_VERSION = `"$ReleaseVersion`""
    Set-Regex "TournamentAssistantShared/Constants.cs" 'SERVER_VERSION_CODE = \d+' "SERVER_VERSION_CODE = $VersionCode"
    Set-Regex "TournamentAssistantShared/Constants.cs" 'MASTER_SERVER = "[^"]+"' "MASTER_SERVER = `"$HostName`""
    Set-Regex "TournamentAssistantShared/Constants.cs" 'MASTER_PORT = \d+' "MASTER_PORT = $RawPort"
    Set-Regex "TournamentAssistantShared/Constants.cs" 'MASTER_API_PORT = \d+' "MASTER_API_PORT = $PublicApiPort"

    Set-Regex "TournamentAssistantServer/TournamentAssistantServer.csproj" '<Version>[^<]+</Version>' "<Version>$ReleaseVersion</Version>"
    Set-Regex "TournamentAssistantServer/TournamentAssistantServer.csproj" '<AssemblyVersion>[^<]+</AssemblyVersion>' "<AssemblyVersion>$ReleaseVersion.0</AssemblyVersion>"
    Set-Regex "TournamentAssistantServer/TournamentAssistantServer.csproj" '<FileVersion>[^<]+</FileVersion>' "<FileVersion>$ReleaseVersion.0</FileVersion>"
    Set-Regex "TournamentAssistant/manifest.json" '"version"\s*:\s*"[^"]+"' "`"version`": `"$ReleaseVersion`""

    Set-Regex "TournamentAssistantStandalone/include/TA/Constants.hpp" 'kServerHost = "[^"]+"' "kServerHost = `"$HostName`""
    Set-Regex "TournamentAssistantStandalone/include/TA/Constants.hpp" 'kServerPort = \d+' "kServerPort = $RawPort"
    Set-Regex "TournamentAssistantStandalone/include/TA/Constants.hpp" 'kMasterApiPort = \d+' "kMasterApiPort = $PublicApiPort"
    Set-Regex "TournamentAssistantStandalone/include/TA/Constants.hpp" 'kClientVersion = \d+' "kClientVersion = $VersionCode"
    Set-JsonVersion "TournamentAssistantStandalone/qpm.json" @("info", "version") $ReleaseVersion
    Set-JsonVersion "TournamentAssistantStandalone/qpm.shared.json" @("config", "info", "version") $ReleaseVersion
    Set-Regex "TournamentAssistantStandalone/mod.template.json" '"version"\s*:\s*"[^"]+"' "`"version`": `"$ReleaseVersion`""
    if (Test-Path (Join-Path $repoRoot "TournamentAssistantStandalone/mod.json")) {
        Set-Regex "TournamentAssistantStandalone/mod.json" '"version"\s*:\s*"[^"]+"' "`"version`": `"$ReleaseVersion`""
    }

    Set-Regex "TournamentAssistantClient/src/constants.ts" 'masterAddress = "[^"]+"' "masterAddress = `"$HostName`""
    Set-Regex "TournamentAssistantClient/src/constants.ts" 'masterPort = "[^"]+"' "masterPort = `"$PublicWebsocketPort`""
    Set-Regex "TournamentAssistantClient/src/constants.ts" 'masterApiPort = "[^"]+"' "masterApiPort = `"$PublicApiPort`""
    Set-Regex "TournamentAssistantClient/src/constants.ts" 'version = "[^"]+"' "version = `"$ReleaseVersion`""
    Set-Regex "TournamentAssistantClient/src/constants.ts" 'versionCode = \d+' "versionCode = $VersionCode"
    Set-Regex "TournamentAssistantClient/src/scraper.ts" 'MASTER_ADDRESS = "[^"]+"' "MASTER_ADDRESS = `"$HostName`""
    Set-JsonVersion "TournamentAssistantClient/package.json" @("version") $ReleaseVersion
    Set-JsonVersion "TournamentAssistantProtos/Scripts/package.json" @("version") $ReleaseVersion
    Set-JsonVersion "TournamentAssistantUI/package.json" @("version") $ReleaseVersion
    Set-JsonVersion "TournamentAssistantUI/src-tauri/tauri.conf.json" @("package", "version") $ReleaseVersion
    Set-Regex "TournamentAssistantUI/src/lib/constants.ts" 'versionName = "[^"]+"' "versionName = `"$ReleaseVersion`""
    Set-Regex "TournamentAssistantUI/src/lib/constants.ts" 'versionCode = \d+' "versionCode = $VersionCode"
    Set-Regex "TournamentAssistantUI/src-tauri/Cargo.toml" '(?m)^version = "[^"]+"' "version = `"$ReleaseVersion`""
}

$ServerUrl = Read-Required "TA server URL (for example https://server.example.com)" $ServerUrl
if ($ServerUrl -notmatch '^[a-zA-Z][a-zA-Z0-9+.-]*://') { $ServerUrl = "https://$ServerUrl" }
$serverUri = [Uri]$ServerUrl
if (-not $serverUri.Host) { throw "Server URL has no hostname: $ServerUrl" }
$Version = Read-Required "Release version (for example 1.3.0)" $Version
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw "Version must be major.minor.patch" }
$versionCode = [int]$Matches[1] * 1000 + [int]$Matches[2] * 100 + [int]$Matches[3] * 10

if ($DeploymentMode -eq "Prompt") {
    $choice = Read-Choice "Deployment mode: direct or proxy" @("direct", "proxy") "direct"
    $DeploymentMode = if ($choice -eq "direct") { "DirectTls" } else { "ReverseProxy" }
}
if ($DeploymentMode -eq "ReverseProxy") {
    $publicPort = if ($serverUri.IsDefaultPort) { 443 } else { $serverUri.Port }
    $publicWebsocketPort = $publicPort
    $publicApiPort = $publicPort
} else {
    $publicWebsocketPort = $WebsocketPort
    $publicApiPort = $ApiPort
}

Require-Command "git"
Require-Command "pwsh"
if (-not $SkipCertificates) { Require-Command "openssl" }
if (-not $SkipClient) { Require-Command "npm"; Require-Command "protoc" }
if (-not $SkipUi) { Require-Command "npm"; Require-Command "cargo" }
if (-not $SkipStandalone) { Require-Command "qpm" }
if (-not $NoDockerBuild) { Require-Command "docker" }
if ($NativeServerPublish) { Require-Command "dotnet" }
if (-not $SkipPcvr) { Require-Command "dotnet" }

Stamp-Sources $serverUri.Host $Version $versionCode $publicWebsocketPort $publicApiPort
if (-not $SkipCertificates) { Configure-Certificates $CertificateMode $CertificateImportPath $serverUri.Host }
Write-ServerConfig $serverUri.Host $DeploymentMode $publicWebsocketPort

$envPath = Join-Path $repoRoot ".env"
$webBind = if ($DeploymentMode -eq "DirectTls") { "0.0.0.0" } else { "127.0.0.1" }
@"
TA_VERSION=$Version
TA_RAW_BIND=0.0.0.0
TA_RAW_PORT=$RawPort
TA_WEBSOCKET_BIND=$webBind
TA_WEBSOCKET_PORT=$WebsocketPort
TA_API_BIND=$webBind
TA_API_PORT=$ApiPort
TA_OAUTH_BIND=127.0.0.1
TA_OAUTH_PORT=8677
"@ | Set-Content $envPath -Encoding utf8

$artifactDir = Join-Path $artifactsRoot $Version
if (Test-Path $artifactDir) { Remove-Item $artifactDir -Recurse -Force }
New-Item -ItemType Directory -Path $artifactDir | Out-Null

if (-not $SkipClient) {
    Write-Section "Generating protobuf TypeScript and building TA client"
    $protoScripts = Join-Path $repoRoot "TournamentAssistantProtos/Scripts"
    if (-not $NoRestore) { Invoke-Step $protoScripts "npm" @("install") }
    Invoke-Step $protoScripts "pwsh" @("./build_proto_for_ts.ps1")
    Invoke-Step $protoScripts "pwsh" @("./copy_ts_to_typescript_client.ps1")
    $clientDir = Join-Path $repoRoot "TournamentAssistantClient"
    Invoke-Step $clientDir "npm" @("version", $Version, "--no-git-tag-version", "--allow-same-version")
    if (-not $NoRestore) { Invoke-Step $clientDir "npm" @("ci") }
    Invoke-Step $clientDir "npm" @("run", "build")
    Copy-Item (Join-Path $clientDir "dist") (Join-Path $artifactDir "TournamentAssistantClient") -Recurse
}

if (-not $SkipUi) {
    Write-Section "Building TournamentAssistantUI web and desktop artifacts"
    $uiDir = Join-Path $repoRoot "TournamentAssistantUI"
    Invoke-Step $uiDir "npm" @("version", $Version, "--no-git-tag-version", "--allow-same-version")
    if (-not $NoRestore) { Invoke-Step $uiDir "npm" @("ci") }
    Invoke-Step $uiDir "npm" @("run", "tauri:build")

    $uiArtifactDir = Join-Path $artifactDir "TournamentAssistantUI"
    $uiWebBuild = Join-Path $uiDir "build"
    $uiDesktopBundle = Join-Path $uiDir "src-tauri/target/release/bundle"
    if (-not (Test-Path $uiWebBuild)) { throw "TAUI web build output is missing: $uiWebBuild" }
    if (-not (Test-Path $uiDesktopBundle)) { throw "TAUI desktop bundle output is missing: $uiDesktopBundle" }
    New-Item -ItemType Directory -Force -Path $uiArtifactDir | Out-Null
    Copy-Item $uiWebBuild (Join-Path $uiArtifactDir "Web") -Recurse
    Copy-Item $uiDesktopBundle (Join-Path $uiArtifactDir "Desktop") -Recurse
    foreach ($binaryName in @("taui", "taui.exe")) {
        $binary = Join-Path $uiDir "src-tauri/target/release/$binaryName"
        if (Test-Path $binary) { Copy-Item $binary $uiArtifactDir -Force }
    }
}

if (-not $SkipStandalone) {
    Write-Section "Building standalone Quest mod"
    $standaloneDir = Join-Path $repoRoot "TournamentAssistantStandalone"
    if (-not $NoRestore) { Invoke-Step $standaloneDir "qpm" @("restore") }
    Invoke-Step $standaloneDir "qpm" @("s", "qmod")
    $qmodPath = Join-Path $standaloneDir "TournamentAssistant Standalone.qmod"
    Test-Qmod $qmodPath $Version (Join-Path $standaloneDir "build/libTAStandalone.so")
    Copy-Item $qmodPath (Join-Path $artifactDir "TournamentAssistant-Standalone-$Version.qmod") -Force
}

if (-not $SkipPcvr) {
    Write-Section "Building PCVR compatibility matrix"
    $pcvrArguments = @(
        "-NoLogo", "-NoProfile", "-File", (Join-Path $repoRoot "scripts/build-pcvr.ps1"),
        "-RepoRoot", $repoRoot,
        "-GameVersion", $PcvrGameVersion,
        "-PluginVersion", $Version,
        "-BeatSaberBaseDir", $BeatSaberBaseDir
    )
    if ($PcvrReferencesRoot) { $pcvrArguments += @("-ReferencesRoot", $PcvrReferencesRoot) }
    Invoke-Step $repoRoot "pwsh" $pcvrArguments
}

if ($NativeServerPublish) {
    Write-Section "Publishing native server"
    $serverProject = Join-Path $repoRoot "TournamentAssistantServer/TournamentAssistantServer.csproj"
    if (-not $NoRestore) { Invoke-Step $repoRoot "dotnet" @("restore", $serverProject) }
    Invoke-Step $repoRoot "dotnet" @("publish", $serverProject, "-c", "Release", "-o", (Join-Path $artifactDir "server"), "/p:UseAppHost=false")
}

if (-not $NoDockerBuild) {
    Write-Section "Building server container"
    Invoke-Step $repoRoot "docker" @("compose", "-f", "compose.yml", "build")
}

Write-Section "Complete"
Write-Host "Version:       $Version ($versionCode)" -ForegroundColor Green
Write-Host "Server:        $($serverUri.Host)" -ForegroundColor Green
Write-Host "Mode:          $DeploymentMode" -ForegroundColor Green
Write-Host "Artifacts:     $artifactDir" -ForegroundColor Green
Write-Host "Compose config: $envPath" -ForegroundColor Green
Write-Host "Start server:  docker compose -f compose.yml up -d" -ForegroundColor Green
