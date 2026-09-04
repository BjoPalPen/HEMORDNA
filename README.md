# HEMORDNA

Hemmets organisationsapp – en svensk PWA för hushållsplanering.

> Hemordna ska hjälpa en eller flera personer i ett hushåll att veta vad som behöver göras,
> vem som ansvarar och vad som är rimligt att göra idag.

- Produktbeskrivning: [docs/PRODUCT.md](docs/PRODUCT.md)
- Arkitektur och beslutsstatus: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Arbetsregler för Claude Code: [CLAUDE.md](CLAUDE.md)

## Kom igång

Repot kan köras antingen i devcontainer/Codespaces eller direkt på en lokal maskin.

Utvecklingsportarna är desamma i båda fallen:

| Del | URL |
|---|---|
| API | <http://localhost:5199> |
| Klient | <http://localhost:5200> |
| PostgreSQL | `localhost:5432` |

### Alternativ 1 – devcontainer / Codespaces

PostgreSQL startar med miljön via `.devcontainer/docker-compose.yml`, och connection string
sätts som miljövariabeln `ConnectionStrings__Hemordna`.

### Alternativ 2 – lokalt (Windows, macOS, Linux)

Förutsätter [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) och
[Docker Desktop](https://www.docker.com/products/docker-desktop/).

```bash
git clone https://github.com/BjoPalPen/HEMORDNA.git
cd HEMORDNA
dotnet tool install --global dotnet-ef --version 10.0.11
```

Starta databasen. `docker-compose.yml` i repo-roten publicerar PostgreSQL på
`127.0.0.1:5432` med samma utvecklingsvärden som devcontainern använder:

```bash
docker compose up -d db
```

Connection string för lokal utveckling ligger redan i
`src/Hemordna.Api/appsettings.Development.json`, så den behöver inte sättas.

#### Om port 5432 redan är upptagen

Har du en PostgreSQL installerad på maskinen sedan tidigare vägrar containern starta:

```text
bind: address already in use
```

Välj då en annan värdport. Skapa en `.env` i repo-roten – den är gitignorerad:

```text
POSTGRES_HOST_PORT=5433
```

Databasen inuti containern lyssnar fortfarande på 5432; det är bara porten på din maskin som
flyttas. Peka sedan API:t dit. Miljövariabeln slår värdet i `appsettings.Development.json`:

```powershell
$env:ConnectionStrings__Hemordna =
    "Host=localhost;Port=5433;Database=hemordna;Username=hemordna;Password=hemordna_dev"
```

Variabeln gäller bara i den terminalen, så den måste sättas i varje fönster där du kör
`dotnet ef` eller `dotnet run --project src/Hemordna.Api`. Vill du slippa det, ändra `Port=`
i `appsettings.Development.json` i stället – men den filen är versionshanterad.

### JWT-signeringsnyckel

API:t vägrar starta utan en signeringsnyckel, och den ska aldrig checkas in. Nyckeln måste
vara minst 32 byte. Lagra den i user secrets – en gång per maskin, utanför repot:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<minst 32 tecken>" --project src/Hemordna.Api
```

Generera ett värde med `openssl rand -base64 48` (bash) eller, i PowerShell:

```powershell
$key = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
dotnet user-secrets set "Jwt:SigningKey" $key --project src/Hemordna.Api
```

Miljövariabeln `Jwt__SigningKey` fungerar också och har högre prioritet än user secrets.

### Applicera migrationer och starta

```bash
dotnet ef database update \
  --project src/Hemordna.Infrastructure \
  --startup-project src/Hemordna.Api
```

API och klient är två processer och körs i varsin terminal:

```bash
dotnet run --project src/Hemordna.Api
dotnet run --project src/Hemordna.Client
```

Klienten öppnas på <http://localhost:5200> och läser API:ts adress från
`src/Hemordna.Client/wwwroot/appsettings.json`.

I Development-miljö seedas ett litet demohem automatiskt vid API-start. Logga in med:

```text
E-post: demo@hemordna.local
Lösenord: Hemordna-demo-2026!
```

Kontot har hushållet "Demohemmet", medlemmen Alex, områdena Kok och Badrum samt ett par
uppgifter. Seedningen är återkörningssäker och påverkar inte andra miljöer.

## Bygg och testa

```bash
dotnet build Hemordna.slnx
dotnet test Hemordna.slnx
```

E2E-testerna driver en riktig webbläsare och startar själva API och klient om inget redan
lyssnar på portarna. Playwrights webbläsare installeras en gång:

```bash
dotnet build tests/Hemordna.E2E.Tests
pwsh tests/Hemordna.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

## Struktur

```text
src/Hemordna.Domain          entiteter, value objects, domänregler
src/Hemordna.Application     use cases, DailyPlanner
src/Hemordna.Infrastructure  EF Core, PostgreSQL, Identity
src/Hemordna.Api             Minimal APIs, JWT, DTO:er
src/Hemordna.Client          Blazor WebAssembly PWA
```
