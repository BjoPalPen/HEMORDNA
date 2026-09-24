# Överlämning

Lägesbild per 2026-09-24. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` pushad, kodrelease `5281a08` deployad till https://app.hemordna.se. Tolv PR (#23–#34)
sedan `21410b4`, fem med datamigrering.

- **Påminnelser** (PR #27–#30) – restid, två notiser, gallring efter 30 dagar, och avbockning som
  ALDRIG räknas som hushållsarbete.
- **Notis efter ändrad tid** (PR #31). Nyckeln är `(ReminderId, Kind, ScheduledFor)`; förut blev
  en flyttad påminnelse tyst för alltid. `MoveReminder` m.fl. medvetet orörda – de ska inte veta
  något om push. Tidpunkten normaliseras till hela minuter (exakt likhet mot µs).
- **Nedräkning mot avgång** (PR #32). Staplar, en per 5 min, inom 60 min, bara närmaste.
  **Kvantumet är fast** – takten är informationen. Nytt projekt `Hemordna.Client.Tests`, §8.
- **Realtid blockerar inte längre sidan** (PR #33). `ConnectAsync` kastade, `OnInitializedAsync`
  fångade inte, döda anslutningen låg kvar – sidan frös permanent på "Hämtar din dag…". `Speech`
  hade samma fel. Båda felvägarna saknar seam och är medvetet otestade.
- **Avgångsnotisen i tid** (PR #34). Driftrapport: 4 min 44 s sen. Svepet går nu varje minut och
  notisen skickas `PrepareMinutes = 5` FÖRE avgång – man kan behöva klä på sig. Brödtexten bär
  avgångstiden, annars ljuger en sent läst notis. `TravelMinutes` orörd; Min dag visar avgången.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker Desktop
krävs lokalt – annars faller ALLA E2E på "did not become reachable". Server:
`ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy: `git pull
--ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d --build --no-deps
hemordna-api`; migrationer körs vid uppstart, Postgres heter `hemordna-postgres-1`, och en
allow-regel i `settings.json` går före auto-lägets spärr. **VAPID-nycklarna: serverns `.env`.**

## Verifierat

Build 0 fel. Domain 267/267, Application 451/451, Client 13/13. E2E 219/220 på 18 min. Produktion:
RestartCount 0, `polling every 00:01:00` i loggen, health Stockholm, smoke PASS, notiser sedda på
riktig iPhone.

## Öppna frågor

**E2E föll slumpmässigt på sidladdning** – 1–7 röda per körning, olika tester, alla med
`Hämtar din dag…`. **Efter att 14 worktrees (3,3 GB) städats bort: noll sådana fel, och sviten gick
på 18 min mot 23–27.** En körning bevisar inget, men det är enda åtgärden som bitit. **Rättelse:**
bedömningen att `SkarmbilderTests` är "äkta flakigt, oberoende av belastning" är tillbakadragen –
aldrig belagd, och PR #33 är inte heller bevisad som orsak. Vill man veta säkert: fånga
webbläsarkonsolen i fixturen. `TaskGroupingTests` faller ibland på delad databasstatus men
passerar isolerat. `Reconnected`-closuren fångar `householdId`; byter någon hushåll rejoinas det
gamla. Gallringen har ännu inte raderat något – äldsta påminnelsen i prod är 2026-09-23.
Rollback-taggar har prefixet `rollback-before-`. Kvar sedan tidigare: etiketten "Tid i förväg".
