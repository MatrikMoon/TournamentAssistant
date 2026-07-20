# TournamentAssistant deployment and release build

The release builder produces the server container, TypeScript client, TournamentAssistantUI web/desktop packages, Quest QMOD, and the complete PCVR compatibility matrix. The same script can prepare either a direct-TLS deployment or a reverse-proxy deployment.

| Host port | Container protocol | Purpose |
| ---: | --- | --- |
| 8675 | Raw TLS/TCP | Game clients and replay publishers |
| 8676 | WS or WSS | TA clients and `/live/u/{platformId}` viewers |
| 8677 | Plain HTTP | Optional legacy Discord OAuth callback; disabled by default |
| 8678 | HTTP or HTTPS | REST API, Swagger, and file server |

Port 8675 is not HTTP and must never be sent through an HTTP reverse proxy. Forward it directly or use layer-4 TCP passthrough.

All platforms need Git, PowerShell 7 (`pwsh`), OpenSSL, Docker with Compose v2, Node.js/npm, `protoc`, Rust/Cargo for TournamentAssistantUI, QPM-RS and the Android NDK/toolchain for Quest, plus the .NET SDK and Beat Saber reference assemblies for PCVR. PCVR compilation should normally run on the Windows machine containing the supported Beat Saber installations.

Ubuntu/Debian host tools:

```bash
sudo apt-get update
sudo apt-get install -y git openssl protobuf-compiler ca-certificates curl

docker --version
docker compose version
pwsh --version
node --version
npm --version
protoc --version
qpm --version
```

Initialize a new clone with submodules. Do this before creating local submodule branches:

```bash
git clone --recurse-submodules https://github.com/MatrikMoon/TournamentAssistant
cd TournamentAssistant
git submodule update --init --recursive
```

From the repository root:

```bash
pwsh ./scripts/build-deployment.ps1
```

The script asks for:

1. the public TA server URL, such as `https://server.ta.artemis.shyyluna.dev`;
2. the release version, such as `1.3.0`;
3. direct TLS or reverse-proxy deployment;
4. whether to generate, import, or retain certificates.

It then:

- stamps the server address and version into Shared, Server, PCVR metadata, Quest, protobuf tooling, and the TypeScript client;
- generates the TypeScript protobuf files;
- builds `TournamentAssistantClient`;
- builds TournamentAssistantUI as static web output and native Tauri bundles;
- builds and packages the standalone `.qmod`;
- builds all six supported PCVR targets without switching branches;
- writes persistent deployment configuration;
- builds the server Docker image using [compose.yml](compose.yml).

Artifacts are written under:

```text
Artifacts/<version>/TournamentAssistantClient/
Artifacts/<version>/TournamentAssistantUI/Web/
Artifacts/<version>/TournamentAssistantUI/Desktop/
Artifacts/<version>/TournamentAssistant-Standalone-<version>.qmod
Artifacts/<version>/PCVR/
```

Unlike the old `build-all-versions.ps1`, shared generation/build steps run once and PCVR builds never switch or merge branches.

### Fully non-interactive examples

Import production certificates and terminate TLS inside TA:

```bash
pwsh ./scripts/build-deployment.ps1 \
  -ServerUrl https://server.ta.artemis.shyyluna.dev \
  -Version 1.3.0 \
  -DeploymentMode DirectTls \
  -CertificateMode Import \
  -CertificateImportPath /secure/ta-certificates
```

Generate local development certificates:

```bash
pwsh ./scripts/build-deployment.ps1 \
  -ServerUrl https://localhost \
  -Version 1.3.0 \
  -DeploymentMode DirectTls \
  -CertificateMode Generate
```

Use a reverse proxy and retain existing `deployment/files` certificates:

```bash
pwsh ./scripts/build-deployment.ps1 \
  -ServerUrl https://server.ta.artemis.shyyluna.dev \
  -Version 1.3.0 \
  -DeploymentMode ReverseProxy \
  -CertificateMode Keep
```

Useful switches:

```text
-NoRestore             Do not run npm install/ci or qpm restore
-NoDockerBuild         Prepare/build clients without building the server image
-NativeServerPublish   Also create a native dotnet publish artifact
-SkipStandalone        Skip the Quest build
-SkipClient            Skip protobuf generation and the TypeScript client build
-SkipUi                Skip TournamentAssistantUI web and desktop builds
-SkipPcvr              Skip all PCVR builds
-SkipCertificates      Do not prompt for or validate certificates
-PcvrGameVersion VER   Build All or one supported Beat Saber version
-PcvrReferencesRoot    Root containing one reference directory per version
```

## PCVR from one working branch

PCVR no longer needs the build script to check out, merge, or modify six branches. The supported targets are 1.29.1, 1.34.2, 1.39.1, 1.40.8, 1.41.1, and 1.42.0. Keep all new code on the current/master source. The builder combines that source with pinned, immutable compatibility deltas in disposable `.build/pcvr/<game-version>` directories.

The old branches are used only as historical inputs through pinned commit IDs. They are not checked out and do not need new commits. Preserve those objects in the repository (preferably with permanent tags) before deleting remote branches:

```bash
git tag pcvr-compat/base-1.29.1 fe54031472ffe947be442c947ab9f8c0fd08bfda
git tag pcvr-compat/1.34.2 eb13a51ebdac0703dc77995edf75a8242a37d030
git tag pcvr-compat/1.39.1 f67b16ce9cac10c89965e711849658ee6a1b4d19
git tag pcvr-compat/1.40.8 461fc0c2de198dc68d93fe80d3af5919d3cb4784
git tag pcvr-compat/1.41.1 5ba88060117bc6646d6f8b076d546f0c4076ff40
git tag pcvr-compat/1.42.0 444319b0a3f178533e191d622c8b05ae201ad7ad
git push origin 'refs/tags/pcvr-compat/*:refs/tags/pcvr-compat/*'
```

On the Windows build host, either arrange full game installs as:

```text
O:\BSManager\BSInstances\1.29.1\Beat Saber_Data\Managed\
O:\BSManager\BSInstances\1.29.1\Plugins\
...
O:\BSManager\BSInstances\1.42.0\Beat Saber_Data\Managed\
O:\BSManager\BSInstances\1.42.0\Plugins\
```

or create a reference-only root with the same version subdirectories and pass `-ReferencesRoot`.

Build every PCVR target without touching the current branch:

```powershell
pwsh ./scripts/build-pcvr.ps1 `
  -GameVersion All `
  -PluginVersion 1.3.0 `
  -BeatSaberBaseDir 'O:\BSManager\BSInstances'
```

Build one target or inspect its generated source without compiling:

```powershell
pwsh ./scripts/build-pcvr.ps1 -GameVersion 1.40.8 -PluginVersion 1.3.0
pwsh ./scripts/build-pcvr.ps1 -GameVersion 1.42.0 -PluginVersion 1.3.0 -NoBuild -KeepStage
```

Outputs are placed in `Artifacts/<plugin-version>/PCVR/`. The build explicitly disables copying DLLs into game installations. If a future common edit overlaps an old compatibility delta, the preparation step fails instead of silently producing a mixed-version DLL; update the compatibility adapter at that point.

The complete deployment build includes PCVR by default:

```powershell
pwsh ./scripts/build-deployment.ps1 `
  -ServerUrl https://server.ta.example.com `
  -Version 1.3.0 `
  -DeploymentMode DirectTls `
  -CertificateMode Keep `
  -PcvrGameVersion All `
  -BeatSaberBaseDir 'O:\BSManager\BSInstances'
```

# Certificates

The server always requires these files, even when a reverse proxy terminates web TLS:

```text
deployment/files/server.pfx             password: password
deployment/files/player.pfx             password: TAPlayerPass
deployment/files/mock.pfx               password: password
deployment/files/beatkhana-public.pem
```

`server.pfx` secures raw port 8675 and signs server tokens. `player.pfx` supports legacy player and long-lived bot identity. `mock.pfx` supports development authentication. `beatkhana-public.pem` verifies BeatKhana game tokens used by standalone.

### Generate mode

Generate mode creates self-signed server/player/mock certificates using RSA-3072 and creates a placeholder BeatKhana keypair. It is sufficient to start and test the server locally.

The generated `beatkhana-public.pem` cannot verify real BeatKhana tokens. For a real Quest deployment, replace it with BeatKhana's production public key or choose Import mode with the production file set.

### Import mode

The import directory must contain exactly:

```text
server.pfx
player.pfx
mock.pfx
beatkhana-public.pem
```

The script copies and validates all four. Importing directly from `deployment/files` is also supported.

### Public server certificate with Certbot

Point the domain's A/AAAA records at the host and allow inbound TCP 80. Then run:

```bash
sudo apt-get install -y certbot openssl
sudo certbot certonly --standalone \
  --preferred-challenges http \
  --agree-tos \
  --no-eff-email \
  --email YOUR_EMAIL_ADDRESS \
  -d server.ta.example.com

mkdir -p /secure/ta-certificates

sudo openssl pkcs12 -export \
  -out /secure/ta-certificates/server.pfx \
  -inkey /etc/letsencrypt/live/server.ta.example.com/privkey.pem \
  -in /etc/letsencrypt/live/server.ta.example.com/fullchain.pem \
  -name server.ta.example.com \
  -keypbe PBE-SHA1-3DES \
  -certpbe PBE-SHA1-3DES \
  -macalg sha1 \
  -passout pass:password

sudo chown "$(id -u):$(id -g)" /secure/ta-certificates/server.pfx
chmod 600 /secure/ta-certificates/server.pfx
```

Copy the production `player.pfx`, `mock.pfx`, and `beatkhana-public.pem` into `/secure/ta-certificates`, then use Import mode. The explicit PKCS#12 algorithms keep the PFX compatible with the server's .NET Core 3.1 runtime.

## 4. Review generated configuration

The build script creates or updates:

```text
.env
deployment/files/serverConfig.json
deployment/files/FileServerContent/
```

Compose maps the configurable public ports from `.env` onto the server's fixed
internal ports (8675, 8676, 8677, and 8678). A reference file is available at
`.env.example`; normally the build script should create `.env` for you.

Existing Discord secrets in `serverConfig.json` are preserved. A new file contains placeholders. Edit its private copy if Discord is needed:

```json
{
  "discordClientId": "YOUR_CLIENT_ID",
  "discordClientSecret": "YOUR_CLIENT_SECRET",
  "botToken": "YOUR_BOT_TOKEN"
}
```

Never commit `deployment/files`; it contains private keys, databases, uploaded files, and credentials and is excluded by `.gitignore`.

Direct TLS produces settings equivalent to:

```json
{
  "port": "8675",
  "overlayPort": "8676",
  "websocketUseSsl": "True",
  "apiUseSsl": "True",
  "oauthPort": "0"
}
```

Reverse-proxy mode uses `False` for both web SSL settings. Raw port 8675 remains TLS in both modes.

Validate before startup:

```bash
docker compose -f compose.yml config

for file in serverConfig.json server.pfx player.pfx mock.pfx beatkhana-public.pem; do
  test -r "deployment/files/$file" || { echo "Missing deployment/files/$file"; exit 1; }
done

openssl pkcs12 -in deployment/files/server.pfx -passin pass:password -info -noout -legacy
openssl pkcs12 -in deployment/files/player.pfx -passin pass:TAPlayerPass -info -noout -legacy
openssl pkcs12 -in deployment/files/mock.pfx -passin pass:password -info -noout -legacy
```

Omit `-legacy` on OpenSSL 1.1 if it is not recognized.

```bash
docker compose -f compose.yml up -d
docker compose -f compose.yml ps
docker compose -f compose.yml logs -f tournament-assistant-server
```

First startup creates persistent SQLite state beneath `deployment/files`:

```text
TournamentDatabase.db
QualifierDatabase.db
UserDatabase.db
FileServerContent/
```

Stop or restart without deleting data:

```bash
docker compose -f compose.yml stop
docker compose -f compose.yml start
docker compose -f compose.yml restart tournament-assistant-server
```

Expose ports 8675, 8676, and 8678. OAuth 8677 remains loopback-only and disabled unless intentionally configured.

```bash
sudo ufw allow 8675/tcp
sudo ufw allow 8676/tcp
sudo ufw allow 8678/tcp
```

Verify from another machine:

```bash
openssl s_client \
  -connect server.ta.artemis.shyyluna.dev:8675 \
  -servername server.ta.artemis.shyyluna.dev </dev/null

curl -I https://server.ta.artemis.shyyluna.dev:8678/swagger/index.html
websocat wss://server.ta.artemis.shyyluna.dev:8676/live/u/7651234561789123
```

The WebSocket remains connected and silent until that platform ID publishes replay data.

Certbot renews PEM files, not `server.pfx`. A Certbot deploy hook must repeat the PKCS#12 export and then run:

```bash
cd /opt/TournamentAssistant
docker compose -f compose.yml restart tournament-assistant-server
```

In reverse-proxy mode `.env` publishes ports 8676 and 8678 only on `127.0.0.1`; port 8675 remains public. Terminate HTTPS/WSS at Nginx, NPM, or Caddy.

Nginx example:

```nginx
server {
    listen 443 ssl http2;
    server_name server.ta.artemis.shyyluna.dev;

    ssl_certificate     /etc/letsencrypt/live/server.ta.artemis.shyyluna.dev/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/server.ta.artemis.shyyluna.dev/privkey.pem;

    location /api/ {
        proxy_pass http://127.0.0.1:8678;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto https;
    }

    location /swagger/ {
        proxy_pass http://127.0.0.1:8678;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto https;
    }

    location / {
        proxy_pass http://127.0.0.1:8676;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

Public endpoints become:

```text
wss://server.ta.artemis.shyyluna.dev/live/u/PLATFORM_ID
https://server.ta.artemis.shyyluna.dev/api/file/FILE_ID
https://server.ta.artemis.shyyluna.dev/swagger/index.html
```

NPM needs one WebSocket-enabled proxy host forwarding `/` to 8676 and custom locations `/api/` and `/swagger/` forwarding to 8678. Do not configure NPM as an HTTP proxy for 8675.


```bash
pwsh ./scripts/build-deployment.ps1 \
  -ServerUrl https://localhost \
  -Version 1.3.0 \
  -DeploymentMode DirectTls \
  -CertificateMode Generate

docker compose -f compose.yml up -d
docker compose -f compose.yml logs -f tournament-assistant-server
```

Because the certificate is self-signed, browser and desktop clients must trust `deployment/files/server.crt`.

```bash
curl -k -I https://127.0.0.1:8678/swagger/index.html
openssl s_client -connect 127.0.0.1:8675 -servername localhost </dev/null
websocat --insecure wss://127.0.0.1:8676/live/u/test-player
```

Back up the complete persistent directory:

```bash
tar -czf "ta-files-$(date +%Y%m%d-%H%M%S).tar.gz" deployment/files
```

Then rebuild and recreate the container:

```bash
pwsh ./scripts/build-deployment.ps1 \
  -ServerUrl https://server.ta.example.com \
  -Version NEW_VERSION \
  -DeploymentMode DirectTls \
  -CertificateMode Keep

docker compose -f compose.yml up -d --force-recreate
docker compose -f compose.yml logs -f tournament-assistant-server
```

Do not delete `deployment/files` during an update.

## Troubleshooting

- `BIO_new_file:no such file`: a required file is absent from `/app/files`. Run the section 4 file check and inspect `docker compose config`.
- PFX password/algorithm failure: server and mock use `password`; player uses `TAPlayerPass`. Re-export with the SHA1/3DES options above.
- Quest connects but cannot authenticate: a generated placeholder BeatKhana key cannot verify production tokens. Import the real `beatkhana-public.pem`.
- Browser TLS error on 8676/8678: direct mode needs a trusted certificate; proxy mode needs `websocketUseSsl=False` and `apiUseSsl=False` internally.
- Nginx/NPM 502: verify loopback endpoints before debugging the proxy.
- No replay data: enable replay streaming and subscribe with the exact authenticated platform ID.
- Viewer joining mid-song gets no map: the server must be 1.3.0+ so it sends cached start metadata first.
