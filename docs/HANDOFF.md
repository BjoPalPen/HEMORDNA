# Överlämning

Lägesbild per 2026-09-25 (kväll). Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` = `4ff2ea0`, deployad. Tre PR i dag: #35, #36 (påminnelser) och #37 (PWA-uppdateringen).

- **PWA-uppdateringen var trasig** (PR #37). En installerad PWA kunde ligga kvar på ett gammalt
  bygge och prata med ett nytt API. `service-worker-assets.js` – fillistan workern läser med
  `importScripts` – saknade `Cache-Control`, och registreringen hade standardläget
  `updateViaCache: 'imports'`. Webbläsaren serverade då en GAMMAL fillista, nya workern försökte
  cacha förra byggets `_framework`-filer, 404 gav tom kropp, SRI föll och workern kasserades som
  `redundant` – helt tyst. `MapFallbackToFile` hade dessutom egna `StaticFileOptions` UTAN
  `OnPrepareResponse`, så varje klientroute saknade huvudet. Delad `SetNoCacheForAppShellFiles` på
  båda ställena nu. **`_framework/*` ska aldrig få `no-cache`** – eget testfall skyddar det.
- **Synlighet för påminnelser** (PR #35). `ReminderVisibility` i ETT fält: `Private` (default),
  `BusyOnly` (andra ser tiden, aldrig titeln), `Household` (titel och tid). Ägaren väljer i sheetet
  på Min dag, andras tider syns på Vecka. **Platsen delas aldrig på någon nivå** – DTO:n för andras
  tider har inget `Location`-fält alls. `Title` nollas i use casen, så regeln täcks av ett test.
- **Lägg till i mina påminnelser** (PR #36). Mottagaren trycker på en delad rad i Vecka och får en
  egen, FRISTÅENDE kopia. Flyttar ägaren sin tid flyttas inte kopian. **Steg 3 är känt, inte
  byggt:** påminnelse för en medlem utan eget konto. Hela resonemanget för #35 och #36:
  `Beslut: Synlighet för påminnelser` i [ARCHITECTURE.md](ARCHITECTURE.md).

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker Desktop
krävs lokalt – annars faller ALLA E2E på "did not become reachable". Server:
`ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy: `git pull
--ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d --build --no-deps
hemordna-api`; migrationer körs vid uppstart, Postgres heter `hemordna-postgres-1`. **VAPID:
serverns `.env`.** Rollback: `6f454cc` för #37, `b73d89c` för #36, `d35da53` för #35.

## Verifierat

Build 0 fel. Domain 276/276, Application 467/467, Client 24/24, Api 7/7. E2E 226/227 på 19 min 18 s
– enda röda är `SkarmbilderTests`, som passerar isolerat. Produktion: RestartCount 0, health
Healthy, `scripts/Smoke` PASS. #37 mätt i Chromium mot prod, före → efter: `updateViaCache`
`imports` → `none`, cache `hemordna-cache-k4o4KeP5` → `q4SyqAa1`, och `no-cache` på alla fyra
skalfilerna plus `/vecka` och `/okand/123` men INTE på `_framework/*`.

## Öppna frågor

**`CLAUDE.md` §8 räknar upp testprojekten och saknar `tests/Hemordna.Api.Tests`** (nytt i #37,
`WebApplicationFactory`, kräver ingen databas) – Björns beslut att komplettera, medvetet inte gjort.
**E2E-flakigheten är belastningsberoende:** samma gren gav 4 röda på 22 min 51 s när sviten delade
maskin med annat arbete, och 1 rött på 19 min 18 s när den fick köra ensam. Kör den ensam före en
release. **Dev-databasen** hade vuxit till 18 967 hushåll och 78 MB (E2E städar aldrig efter sig);
tömd 2026-09-24, fixturstädning är värd en egen uppgift. Städa även gamla worktrees – två ligger
kvar. `Reconnected`-closuren fångar `householdId`; byter någon hushåll rejoinas det gamla. Kvar
sedan tidigare: etiketten "Tid i förväg".
