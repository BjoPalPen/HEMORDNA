# Överlämning

Läge per 2026-09-25 (kväll). Arbetssätt: [../CLAUDE.md](../CLAUDE.md). Äldre bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` = `82bad6a`, deployad. Fyra PR i dag: #35, #36, #38 (påminnelser), #37 (PWA-uppdateringen).

- **Välj vilka som ser en påminnelse** (PR #38). `Visibility` styr VAD, nya `ReminderAudience` styr VEM:
  `Everyone`, eller `Selected` + en `ReminderShare` per utvald. Samma nivå för alla utvalda; nivå per person
  är bortvalt. **Fail-closed hela vägen:** `Audience` är ett eget fält och inte "tom lista betyder alla", så
  en förlorad rad kan bara smalna av synligheten; `Selected` + tom lista = ingen ser den; klienten skickar
  inget audience-anrop vid "Bara jag", annars hade det återskapat de delningar `ChangeVisibility(Private)`
  just rensat. **EF-queryn och `InMemoryReminderRepository` har identiska predikat** – ändra aldrig det ena
  utan det andra.
- **PWA-uppdateringen var trasig** (PR #37). `service-worker-assets.js` – fillistan workern läser med
  `importScripts` – saknade `Cache-Control`, och registreringen hade `updateViaCache: 'imports'`.
  Webbläsaren gav då en GAMMAL fillista, nya workern hämtade förra byggets `_framework`-filer, 404 fällde
  SRI och workern kasserades som `redundant`, helt tyst. `MapFallbackToFile` saknade dessutom
  `OnPrepareResponse`. Delad `SetNoCacheForAppShellFiles` på båda ställena nu. **`_framework/*` ska aldrig
  få `no-cache`** – eget testfall skyddar det.
- **Synlighet** (#35): `Private` / `BusyOnly` (andra ser tiden, aldrig titeln) / `Household`. **Platsen delas
  aldrig på någon nivå** – DTO:n för andras tider har inget `Location`-fält alls. **#36:** mottagaren kan ta
  en delad tid som en egen, FRISTÅENDE kopia. **Steg 3, ej byggt:** påminnelse för en medlem utan konto.
  Allt: `Beslut: Synlighet för påminnelser` i [ARCHITECTURE.md](ARCHITECTURE.md).

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker Desktop krävs lokalt –
annars faller ALLA E2E på "did not become reachable". Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`,
checkout `~/hemordna`. Deploy: `git pull --ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up
-d --build --no-deps hemordna-api`; migrationer körs vid uppstart, Postgres heter `hemordna-postgres-1`. **VAPID:
serverns `.env`.** Rollback: `4ff2ea0` (#38), `6f454cc` (#37), `b73d89c` (#36).

## Verifierat

Build 0 fel. Domain 290/290, Application 489/489, Client 24/24, Api 7/7. E2E 228/229 på 19 min 38 s ensam på
maskinen – enda röda är `SkarmbilderTests`, verifierad grön isolerat. Produktion: RestartCount 0, health
Healthy, `scripts/Smoke` PASS, `Applying migration '…AddReminderAudienceAndShares'` i loggen, och svepets
SQL läser `r."Audience"`.

## Öppna frågor

**`CLAUDE.md` §8 saknar `tests/Hemordna.Api.Tests`** (nytt i #37) – Björns beslut, medvetet ogjort.
**E2E-flakigheten är belastningsberoende**, nu tre mätpunkter: 4 röda när sviten delade maskin, 1 rött
respektive 1 rött när den fick köra ensam, alla gröna isolerat. **Kör den ensam före release.**
**Dev-databasen** växer obegränsat (E2E städar aldrig); tömd 2026-09-24. Fixturstädning och de tre
kvarliggande worktrees är värda egna uppgifter. Kvar sedan tidigare: `Reconnected`-closuren fångar
`householdId`, och etiketten "Tid i förväg".
