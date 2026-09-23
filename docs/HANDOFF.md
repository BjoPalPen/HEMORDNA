# Överlämning

Lägesbild per 2026-09-23. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` är pushad och kodrelease `4ec6c59` deployad till https://app.hemordna.se. Fyra PR
(#23–#26) sedan `21410b4`, varav en med datamigrering.

- **Serverns "idag" är Europe/Stockholm** (PR #26). `HouseholdClock.Today(TimeProvider)` är enda
  källan; 15 serverhärledda datum pekar dit. Zonen är hårdkodad med flit — containern kör UTC
  utan `TZ`, så `GetLocalNow()` där ÄR UTC och hade behållit buggen. Värst var inte fallbacken:
  `CreateTaskDefinition` ankrade nya uppgifters `RecurrenceRule.StartDate` på serverns dag utan
  att kunna ta emot klientens, så en uppgift skapad efter midnatt fick fel veckodag för alltid.
  `POST /tasks` tar nu emot `today`. Två klientbuggar på köpet: `preferred-weekday` skickade
  aldrig sitt `Today`-fält, och `TaskOptionsSheet` läste `DateTime.UtcNow` direkt.
- **Påminnelser om egna tider** (PR #24). Läkarbesök och möten, överst på Min dag och på
  Vecka. Reglerna: PRODUCT.md §11. **Sekretessgränsen skiljer den från allt annat i repot:**
  privat även inom hushållet, så `HouseholdId` räcker inte — varje use case jämför även
  `MemberId` mot anroparen, och "tillhör någon annan" är oskiljaktigt från "finns inte"
  (`null` → `404`). `Title`/`Location` loggas aldrig. Inga notiser ännu — nästa etapp.
- **Hela E2E-sviten härleder dagen lokalt** (PR #25), via `AppDate.Today`.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`.
Docker Desktop måste köra för lokal API/E2E — utan den faller ALLA 217 E2E med
`did not become reachable at localhost:5199`, vilket ser ut som en katastrofal regression men
är tom miljö. LAN: `--urls "http://*:PORT"`.
Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`.
Deploy: `git pull --ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml
up -d --build --no-deps hemordna-api` – migrationer körs vid uppstart, kontrollera
`docker logs hemordna-api` efter en schemaändring. Den ofarliga `libgssapi_krb5.so.2`-varningen
ignoreras. Postgres heter `hemordna-postgres-1`. En allow-regel för deploy-ssh ligger i
användarens `settings.json` och går före auto-lägets `[Production Deploy]`-spärr – verifierat,
Shift+Tab behövs inte längre. `/health` svarar JSON med en kontroll per rad, inklusive zonen.
Browserkontroll: `dotnet run --project scripts/Smoke -- https://app.hemordna.se`.

## Verifierat

Build 0 fel/varningar. Domän 229/229, Application 389/389, E2E 217/217. Regressionstesterna
bevisades falla före fixen (`Expected: 2026-07-01 / Actual: 2026-07-08`); ett tidigare
testförsök passerade i båda fallen eftersom veckodagsavrundningen åt upp endagsskillnaden –
läs kommentaren i testet innan det ändras. Produktion: `AddReminders` applicerad, health
`timezone: Healthy (Europe/Stockholm)`, RestartCount 0, smoke PASS.

## Drift och kvarstående frågor

Rollback-taggar finns: `rollback-before-725fedc`, `rollback-before-4ec6c59`. Floor-releasens
`Down()` återskapar inte Floor+Name – en rollback förbi den kräver eftertanke.
`HouseholdClock`s fallback-gren (UTC när zonen saknas) är inte enhetstestad; hälsokontrollen och
en `Critical`-logg är mitigeringen. `rebalance-assignments`/`activity/daily-summary` använder
medvetet serverns eget datum (ARCHITECTURE.md "Beslut"-avsnittet, rad ~2718). `EnergyTests` kan
falla vid kallstart på en 5-sekunderstimeout – passerar isolerat. Kvar sedan tidigare:
"Tid i förväg"-etiketten och en gles Heavy-uppgift i ett sammanslaget besök. Påminnelserna är
ännu inte sedda på en riktig iPhone.
