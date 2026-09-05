# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på GitHub, PR #9 mergad. **Hemordna kör i produktion**: `https://app.hemordna.se`,
Hetzner-server `62.238.45.45`, samma server som BowlingPlatform (delad Caddy, nätverket
`bowling-edge`). Deploy: `git pull && docker compose -f docker-compose.prod.yml up -d --build`
i `~/hemordna` som `deploy`-kontot. Ny `Dockerfile` bygger både API och klient - API:t
serverar klientens `wwwroot` (`UseStaticFiles`/`MapFallbackToFile`), så Caddy bara behöver en
uppström per domän.

**Tre produktionsbuggar hittade och fixade första driftsättningen** (alla via riktiga
Playwright-körningar mot `https://app.hemordna.se`, inte gissningar):

1. `UseStaticFiles()` utan egen `ContentTypeProvider` 404:ar `.dat`/`.blat`/`.wasm` -
   WASM-runtimens SRI-koll misslyckas och hela appen kraschar på laddningsskärmen.
2. `wwwroot/appsettings.json`s dev-förval (`http://localhost:5199/`) lästes även i produktion
   (Blazor WASM defaultar till Production-miljö utan dev-server) - klienten ropade på
   besökarens egen dator. `appsettings.Production.json` blankar nu värdet; `Program.cs`
   faller tillbaka på `HostEnvironment.BaseAddress`.
3. **Allvarligast:** Hemordnas compose-tjänst hette `api`, samma generiska namn som
   BowlingPlatforms egen tjänst på det delade `bowling-edge`-nätet - Dockers interna DNS blev
   tvetydig och BowlingPlatforms trafik routades tyst till Hemordna. Fixat genom att döpa om
   till `hemordna-api` (docker-compose.prod.yml). **Lärdom: en tjänst på ett delat externt
   nätverk får aldrig ett generiskt namn ("api", "db", "web") - alltid appnamnet.**

**Ny funktion: gå med i ett hushåll via kod.** `Household.InviteCode` (se ARCHITECTURE.md §4),
`JoinHousehold`-use case, `POST /api/households/join`, "Har du en inbjudningskod?"-toggle på
`SkapaHushall.razor`, kod + "Skapa ny kod" på Hushåll-sidan. 245 tester gröna (Domain 74,
Application 98, E2E 42 - 1 känd flaky, se nedan).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**PostgreSQL på port `5433`, inte `5432`** (`.env`). **LAN-åtkomst:** binda med
`--urls "http://*:PORT"`, inte `0.0.0.0`.

## Kända brister

`HushallTests.Changing_a_members_role_...` flakig under full parallell körning (passerar
isolerat), trots tidigare fix (sekventiella anrop i `Hushall.razor`) - ej vidare utrett.

## Öppna frågor och nästa steg

Väckt men **inte påbörjad**: uppskatta städbehov utifrån antal rum/medlemmar/husdjur.
**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll (ARCHITECTURE.md §4).
