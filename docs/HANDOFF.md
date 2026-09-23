# Överlämning

Lägesbild per 2026-09-23. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` pushad, kodrelease `4ec6c59` deployad till https://app.hemordna.se. Fyra PR (#23–#26)
sedan `21410b4`, varav en med datamigrering.

- **Serverns "idag" är Europe/Stockholm** (PR #26). `HouseholdClock.Today(TimeProvider)` är enda
  källan; 15 serverhärledda datum pekar dit. Hårdkodad zon med flit — containern kör UTC utan
  `TZ`, så `GetLocalNow()` där ÄR UTC. Värst var inte fallbacken: `CreateTaskDefinition` ankrade
  nya uppgifters `RecurrenceRule.StartDate` på serverns dag – en uppgift skapad efter midnatt
  fick fel veckodag för alltid. `POST /tasks` tar nu emot
  `today`; två klientbuggar som aldrig skickade sitt datum är också åtgärdade.
- **Påminnelser om egna tider** (PR #24). Läkarbesök och möten, överst på Min dag och på Vecka.
  Reglerna: PRODUCT.md §11. **Sekretessgränsen skiljer den från allt annat i repot:** privat
  även inom hushållet, så `HouseholdId` räcker inte — varje use case jämför även `MemberId` mot
  anroparen, och "tillhör någon annan" är oskiljaktigt från "finns inte". `Title`/`Location`
  loggas aldrig. Inga notiser ännu.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`.
Docker Desktop krävs för lokal API/E2E – utan den faller ALLA 217 E2E på "did not become
reachable", vilket ser ut som en regression men är tom miljö.
Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy:
`git pull --ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d
--build --no-deps hemordna-api` – migrationer körs vid uppstart, kontrollera
`docker logs hemordna-api` efter en schemaändring. `libgssapi_krb5.so.2`-varningen ignoreras.
Postgres heter `hemordna-postgres-1`. En allow-regel för deploy-ssh i användarens
`settings.json` går före auto-lägets `[Production Deploy]`-spärr – verifierat, Shift+Tab behövs
inte. `/health` svarar JSON med en kontroll per rad, inklusive zonen.
Browserkontroll: `dotnet run --project scripts/Smoke -- https://app.hemordna.se`.

## Verifierat

Build 0 fel/varningar. Domän 229/229, Application 389/389, E2E 217/217. Regressionstesterna
bevisades falla före fixen – ett tidigare försök passerade i BÅDA fallen, läs testets kommentar
innan det ändras. Produktion: health `timezone: Healthy`, RestartCount 0, smoke PASS.

## Drift och kvarstående frågor

Rollback-taggar: `rollback-before-725fedc`, `rollback-before-4ec6c59`. Floor-releasens `Down()`
återskapar inte Floor+Name. `HouseholdClock`s fallback-gren är inte enhetstestad – hälsokontroll
och `Critical`-logg är mitigeringen. `rebalance-assignments`/`activity/daily-summary` använder
medvetet serverns eget datum (ARCHITECTURE.md rad ~2718). Kvar: "Tid i förväg"-etiketten, en
gles Heavy-uppgift i ett sammanslaget besök, och att påminnelserna inte setts på en iPhone.
