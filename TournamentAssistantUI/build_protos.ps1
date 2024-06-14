# Step 1: Run build_proto_for_ts.ps1 in its directory
$protoScriptsDir = "..\TournamentAssistantProtos\Scripts"
$buildProtoScriptPath = Join-Path $protoScriptsDir "build_proto_for_ts.ps1"
if (Test-Path -Path $buildProtoScriptPath) {
    Push-Location $protoScriptsDir
    & .\build_proto_for_ts.ps1
    Pop-Location
    Write-Output "Proto build script completed successfully."
}
else {
    Write-Output "Error: build_proto_for_ts.ps1 not found."
    exit 1
}

# Step 2: Run copy_ts_to_typescript_client.ps1 in the same directory as build_proto_for_ts.ps1
$copyScriptPath = Join-Path $protoScriptsDir "copy_ts_to_typescript_client.ps1"
if (Test-Path -Path $copyScriptPath) {
    Push-Location $protoScriptsDir
    & .\copy_ts_to_typescript_client.ps1
    Pop-Location
    Write-Output "File copy script completed successfully."
}
else {
    Write-Output "Error: copy_ts_to_typescript_client.ps1 not found."
    exit 1
}

# Step 3: Run npm run build in TournamentAssistantTypescriptClient
$taClientDir = "..\TournamentAssistantTypescriptClient"
if (Test-Path -Path $taClientDir) {
    Push-Location $taClientDir
    npm i
    npm run build
    Pop-Location
    Write-Output "NPM build completed successfully."
}
else {
    Write-Output "Error: TournamentAssistantTypescriptClient directory not found."
    exit 1
}

Write-Output "All operations completed successfully."
