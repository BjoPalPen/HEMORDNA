# Överlämning

Lägesbild per 2026-09-23. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` är pushad och kodrelease `725fedc` deployad till https://app.hemordna.se. Tre PR
(#23–#25) sedan `21410b4`, varav en med datamigrering.

- **Påminnelser om egna tider** (PR #24). Läkarbesök och möten, överst på Min dag och på
  Vecka. Reglerna: PRODUCT.md §11, ny. **Sekretessgränsen skiljer den från allt annat i
  repot:** privat även inom hushållet, så `HouseholdId` räcker inte — varje use case jämför
  även `MemberId` mot anroparen, och "tillhör någon annan" är oskiljaktigt från "finns inte"
  (`null` → `404`). `Title`/`Location` loggas aldrig. Inga notiser ännu — nästa etapp.
- **Hela E2E-sviten härleder dagen lokalt** (PR #25). 60 anrop använde UTC medan klienten
  räknar lokalt, vilket gjorde sviten opålitlig varje natt mellan midnatt och 02. Ny
  `AppDate.Today` är enda definitionen. `SkarmbilderTests`, listad som "känd flake", var
  detta — den passerar nu.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`.
Docker Desktop måste köra för lokal API/E2E. LAN: `--urls "http://*:PORT"`.
Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`.
Deploy: `git pull --ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml
up -d --build --no-deps hemordna-api` – migrationer körs vid uppstart, kontrollera
`docker logs hemordna-api` efter en schemaändring. Den ofarliga `libgssapi_krb5.so.2`-varningen
ignoreras. Postgres heter `hemordna-postgres-1`. Auto-läget nekar ssh (`[Production Deploy]`)
– lämna det med Shift+Tab.
Browserkontroll: `dotnet run --project scripts/Smoke -- https://app.hemordna.se`.

## Verifierat

Build 0 fel/varningar. Domän 229/229, Application 383/383, **E2E 217/217 – första helt gröna
körningen**, i det fönster som tidigare fällde den. Sekretessgränsen bevisad med riktiga
HTTP-anrop, två konton i samma hushåll: `404` på varje muterande anrop mot den andres
påminnelse, `409` vid domänbrott. Produktion: `AddReminders` bekräftad i loggen och tabellen
inspekterad; health Healthy, RestartCount 0, smoke PASS.

## Drift och kvarstående frågor

Rollback-tagg finns denna gång: `rollback-before-725fedc`. Floor-releasens `Down()` återskapar
inte Floor+Name – en rollback förbi den kräver eftertanke.
**Öppen, och den mest angelägna:** servern härleder "idag" ur UTC när anroparen inte skickar
ett datum (`HouseholdEndpoints` ~20 ställen, `EnsureOccurrencesGenerated`, `RebalanceSchedule`,
`CreateTaskDefinition`), och produktionscontainern saknar `TZ` – `GetLocalNow()` där *är* UTC.
Klienten skickar sitt lokala datum, så Min dag visas rätt, men det servern beslutar själv kan
nattetid hamna på gårdagen. Kräver en uttalad `Europe/Stockholm` på ett ställe. Kvar sedan
tidigare: "Tid i förväg"-etiketten och en gles Heavy-uppgift i ett sammanslaget besök.
