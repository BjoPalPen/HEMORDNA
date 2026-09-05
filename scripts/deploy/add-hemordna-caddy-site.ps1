<#
.SYNOPSIS
    Lägger till app.hemordna.se i BowlingPlatforms delade Caddyfile på Hetzner-servern,
    och laddar om Caddy - utan driftstopp för bowlingplattformen.

.DESCRIPTION
    Körs av dig (bjorn), inte av deploy-kontot - deploy har bara läsrättighet till
    Caddyfilen med flit, se docs/ARCHITECTURE.md. Skriptet är säkert att köra flera
    gånger: det lägger bara till blocket om det inte redan finns.

    Laddar INTE om Caddy - deploy-kontot kan göra det ofarligt på egen hand (redan
    testat), så det sköts separat efter att du kört det här.

.PARAMETER SshKeyPath
    Sökväg till din privata SSH-nyckel för bjorn@62.238.45.45. Om du loggar in med
    lösenord i stället för nyckel, utelämna denna parameter - ssh frågar då efter
    lösenord interaktivt.

.EXAMPLE
    .\add-hemordna-caddy-site.ps1
    .\add-hemordna-caddy-site.ps1 -SshKeyPath "$HOME\.ssh\hetzner_bowling"
#>
param(
    [string]$SshHost = "62.238.45.45",
    [string]$SshUser = "bjorn",
    [string]$SshKeyPath = "",
    [string]$CaddyfilePath = "/home/bjorn/BowlingPlatform/Caddyfile"
)

$ErrorActionPreference = "Stop"

$sshArgs = @()
if ($SshKeyPath -ne "") {
    if (-not (Test-Path $SshKeyPath)) {
        throw "Hittar ingen nyckel på $SshKeyPath"
    }
    $sshArgs += @("-i", $SshKeyPath)
}
$target = "$SshUser@$SshHost"

function Invoke-RemoteCommand {
    param([string]$Command)
    & ssh @sshArgs $target $Command
    if ($LASTEXITCODE -ne 0) {
        throw "SSH-kommandot misslyckades (exit $LASTEXITCODE): $Command"
    }
}

Write-Host "1) Lägger till app.hemordna.se i Caddyfilen (om den inte redan finns)..." -ForegroundColor Cyan

# Enkelcitat-here-string => $ och { är bokstavliga i PowerShell. Sökvägen sätts in med
# ett vanligt textbyte (.Replace), inte -f, eftersom Caddy-blockets måsvingar annars
# hade krockat med -f-operatorns egna {0}-platshållare.
$remoteScriptTemplate = @'
set -e
CADDYFILE="__CADDYFILE_PATH__"
MARKER="app.hemordna.se {"

if grep -qF "$MARKER" "$CADDYFILE"; then
    echo "Blocket finns redan - hoppar over."
else
    cat >> "$CADDYFILE" <<'BLOCK'

app.hemordna.se {
    encode zstd gzip
    reverse_proxy hemordna-api:8080
}
BLOCK
    echo "Blocket tillagt."
fi
'@

$remoteScript = $remoteScriptTemplate.Replace("__CADDYFILE_PATH__", $CaddyfilePath)

Invoke-RemoteCommand -Command $remoteScript

Write-Host ""
Write-Host "Klart - blocket ligger i Caddyfilen. Säg till, så laddar jag om Caddy och" -ForegroundColor Green
Write-Host "verifierar från deploy-kontot (behöver inte köras här)." -ForegroundColor Green
