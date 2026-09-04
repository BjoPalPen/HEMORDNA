# Överlämning – från Codespace till lokal Windows-miljö

Status per 2026-09-04. Skriven för att läsas in i en ny Claude Code-session på Windows, så
att den sessionen slipper börja från noll.

Läs först [../CLAUDE.md](../CLAUDE.md) – den styr arbetssättet och gäller före det här
dokumentet. Detta är en lägesbild, inte en regelsamling.

---

## 1. Var projektet står

`main` innehåller hela bootstrap-arbetet: domänmodell, planering, persistence, API, klient
och E2E-tester. 111 tester gröna, build utan varningar.

| Lager | Läge |
|---|---|
| `Hemordna.Domain` | `Household`, `HouseholdMember`, `Area`, `TaskDefinition`, `TaskOccurrence`, tidsbudget |
| `Hemordna.Application` | use cases för hushållssammansättning, dagsplan, slutför, skjut upp |
| `Hemordna.Infrastructure` | EF Core mot PostgreSQL, Identity, två migrationer |
| `Hemordna.Api` | Minimal APIs, JWT, `HouseholdAccessFilter`, domänfel som ProblemDetails |
| `Hemordna.Client` | Blazor WASM med `LoggaIn` och `MinDag` |

Detaljerad status per beslut finns i [ARCHITECTURE.md](ARCHITECTURE.md), där varje avsnitt
är märkt `IMPLEMENTED`, `PROPOSED` eller `OPEN`.

### API-yta

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/me
POST /api/households
GET  /api/households/{householdId}
POST /api/households/{householdId}/members
POST /api/households/{householdId}/areas
GET  /api/households/{householdId}/tasks
POST /api/households/{householdId}/tasks
POST /api/households/{householdId}/tasks/{taskId}/occurrences
PUT  /api/households/{householdId}/members/{memberId}/availability
POST /api/households/{householdId}/occurrences/{occurrenceId}/complete
POST /api/households/{householdId}/occurrences/{occurrenceId}/defer
GET  /api/households/{householdId}/members/{memberId}/plan
```

---

## 2. Köra lokalt

Fullständig uppstart finns i [../README.md](../README.md). Sammanfattat:

```powershell
docker compose up -d db
dotnet user-secrets set "Jwt:SigningKey" "<minst 32 tecken>" --project src/Hemordna.Api
dotnet ef database update --project src/Hemordna.Infrastructure --startup-project src/Hemordna.Api
dotnet run --project src/Hemordna.Api      # terminal 1
dotnet run --project src/Hemordna.Client   # terminal 2
```

Portar: API `5199`, klient `5200`, PostgreSQL `5432`.

### Fällor som är specifika för Windows

**Reopen in Container.** Repot innehåller `.devcontainer/`, så VS Code erbjuder sig att öppna
mappen i en container. Tacka nej om avsikten är att köra nativt på Windows.

**Port 5432.** Krockar med en lokalt installerad PostgreSQL. Byt i så fall värdporten i
`docker-compose.yml` och matcha `Port=` i `src/Hemordna.Api/appsettings.Development.json`.

**Två compose-filer.** `docker-compose.yml` i roten är för lokal körning och publicerar
databasen på `127.0.0.1:5432`. `.devcontainer/docker-compose.yml` är devcontainerns egen och
exponerar ingen port utåt – de ska inte blandas ihop.

---

## 3. Konfiguration och hemligheter

Ingen nyckel ligger i repot. API:t vägrar starta utan `Jwt:SigningKey`, som måste vara minst
32 byte.

| Värde | Var det kommer ifrån | Prioritet |
|---|---|---|
| `Jwt:SigningKey` | user secrets lokalt, `Jwt__SigningKey` i miljö | miljö slår user secrets |
| `ConnectionStrings:Hemordna` | `appsettings.Development.json` lokalt | `ConnectionStrings__Hemordna` i miljö slår filen |

Devcontainern sätter connection string som miljövariabel. Därför fungerar båda miljöerna mot
samma repo utan att någon konfiguration behöver ändras vid byte.

---

## 4. Vad som gjordes i sessionen före överlämningen

- Pushade 22 ocommittade-till-remote commits och mergade PR #2 till `main`.
- Gjorde repot körbart utanför devcontainern: ny `docker-compose.yml` i roten, connection
  string i `appsettings.Development.json`, JWT-nyckel via user secrets.
- Rättade utvecklingsportarna. Launch-profilerna hade kvar mallens `5248`/`5154` medan
  klienten och E2E-testerna utgår från `5199`/`5200`. Klienten anropade alltså en port där
  inget lyssnade.
- Lade till `.gitattributes` som normaliserar radslut till LF.

---

## 5. Öppna frågor

Från [ARCHITECTURE.md](ARCHITECTURE.md) avsnitt 10:

| Fråga | Varför den väntar |
|---|---|
| Vem genererar `TaskOccurrence` – on demand eller schemalagt jobb | Beror på recurrence-modellen |
| Hur roterande ansvar räknas ut | Kräver `TaskAssignment` som egen entitet |
| Offline-strategi bortom read-only cache | Utanför MVP |

**Om auth:** scoping är på plats och upprätthålls. `HouseholdAccessFilter` körs före varje
handler på `/api/households/{householdId}`, och ett hushåll som anroparen inte tillhör ger
404 i stället för 403. Det som *inte* finns är flera hushåll per användare – en användare är
i dag medlem i exakt ett. Den utvidgningen är additiv och kräver en egen medlemskapstabell.

### Rimliga nästa steg

1. `RecurrenceRule`, som låser upp occurrence-genereringen och därmed två av de öppna frågorna.
2. SignalR-hub per hushåll, gruppen `household:{id}`, för realtidssynk.
3. `.vscode/launch.json` och `tasks.json` så att API och klient startar tillsammans med F5.
