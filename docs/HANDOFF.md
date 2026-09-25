# Överlämning

Lägesbild per 2026-09-25. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` = `b73d89c`, deployad till https://app.hemordna.se. Senast: PR #35, synlighet för påminnelser.

- **Synlighet för påminnelser** (PR #35, sex commits) – `ReminderVisibility` med tre nivåer i ETT
  fält: `Private` (default), `BusyOnly` (andra ser tiden, aldrig titeln), `Household` (titel och
  tid). Ägaren väljer i sheetet på Min dag; andras tider syns i "Andras tider den här veckan" på
  Vecka. **Platsen delas aldrig på någon nivå** – `HouseholdReminderView`/`-Response` har inget
  `Location`-fält alls, så det finns strukturellt inget att läcka.
- **Eget läsflöde, ingen union.** `GetOwnReminders`, `ReminderResponse` och
  `ListForMemberInRangeAsync` är orörda – andras tider går genom egen repository-metod, egen use
  case och egen smalare DTO, eftersom en union hade gjort varje framtida fält på `ReminderResponse`
  till en läcka. `Title` nollas i use casen, inte i endpointen eller UI:t, så regeln täcks av ett
  Application-test. Resonemanget: `Beslut: Synlighet för påminnelser` i [ARCHITECTURE.md](ARCHITECTURE.md).
- **Bara ägaren agerar.** Ingen kan bocka av, ändra eller avboka någon annans tid. Punktrutnätet på
  Hushåll lämnades medvetet orört: prickarna kodar utfört ARBETE, och en "borta"-markering där hade
  lästs som ogjort. **Steg 2 och 3 är kända, inte byggda:** "lägg till i mina påminnelser" på en
  delad tid, och påminnelse för en medlem utan konto. Deltagare byggs inte alls (CLAUDE.md §12).

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker Desktop
krävs lokalt – annars faller ALLA E2E på "did not become reachable". Server:
`ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy: `git pull
--ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d --build --no-deps
hemordna-api`; migrationer körs vid uppstart, Postgres heter `hemordna-postgres-1`. **VAPID-nycklarna:
serverns `.env`.** Rollbackpunkt för #35: `d35da53` (ingen tagg hann sättas).

## Verifierat

Build 0 fel. Domain 276/276, Application 467/467, Client 17/17. E2E 222/224 på 18 min 38 s – de två
röda (`TaskGroupingTests`, `SkarmbilderTests`) rör inte påminnelser och passerar isolerat.
Produktion: `Applying migration '20260924182633_AddReminderVisibility'` i loggen, RestartCount 0,
health Healthy (Europe/Stockholm), `scripts/Smoke` PASS på 390 och 1280 px, och svepets SQL läser
`r."Visibility"` – kolumnen finns alltså i produktionsdatabasen.

## Öppna frågor

**Dev-databasen hade vuxit till 18 967 hushåll, 66 477 förekomster och 78 MB** – E2E städar aldrig
efter sig. Den tömdes 2026-09-24 (schema och alla migreringar kvar, bara data borta). Det är
rimligen den "delade databasstatus" `TaskGroupingTests` faller på. **Slutar den falla nu är det ett
tecken, och fixturstädning är värd en egen uppgift.** Städa även gamla worktrees – en ligger kvar
(`agent-a2836895c322add0f`); förra gången försvann de slumpmässiga sidladdningsfallen när fjorton
sådana rensades. `Reconnected`-closuren fångar `householdId`; byter någon hushåll rejoinas det
gamla. Kvar sedan tidigare: etiketten "Tid i förväg".
