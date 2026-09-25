# Överlämning

Lägesbild per 2026-09-25. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` = `6f454cc`, deployad till https://app.hemordna.se. Senast: PR #36, lägg till en delad tid.

- **Synlighet för påminnelser** (PR #35) – `ReminderVisibility` i ETT fält: `Private` (default),
  `BusyOnly` (andra ser tiden, aldrig titeln), `Household` (titel och tid). Ägaren väljer i sheetet
  på Min dag, andras tider syns på Vecka. **Platsen delas aldrig på någon nivå** –
  `HouseholdReminderView`/`-Response` har inget `Location`-fält alls, inget att läcka.
- **Eget läsflöde, ingen union.** `GetOwnReminders`, `ReminderResponse` och
  `ListForMemberInRangeAsync` är orörda – andras tider har egen repository-metod, egen use case och
  egen smalare DTO, och `Title` nollas i use casen så regeln täcks av ett Application-test. Bara
  ägaren agerar; punktrutnätet på Hushåll lämnades orört (prickarna kodar utfört ARBETE). Hela
  resonemanget: `Beslut: Synlighet för påminnelser` i [ARCHITECTURE.md](ARCHITECTURE.md).
- **Lägg till i mina påminnelser** (PR #36) – mottagaren trycker på en delad rad i Vecka och får en
  egen, FRISTÅENDE kopia (titel, datum, klockslag – aldrig plats eller restid, alltid `Private`).
  Flyttar ägaren sin tid flyttas inte kopian; en länkad kopia vore början på mötesserier. Rent
  klientarbete. Steg 1:s E2E-assertion "inga knappar alls på andras rader" är medvetet avsmalnad
  till de tre ägar-åtgärderna, med ett nytt test som kräver exakt EN knapp på raden. **Steg 3 är
  känt, inte byggt:** påminnelse för en medlem utan eget konto.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker Desktop
krävs lokalt – annars faller ALLA E2E på "did not become reachable". Server:
`ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy: `git pull
--ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d --build --no-deps
hemordna-api`; migrationer körs vid uppstart, Postgres heter `hemordna-postgres-1`. **VAPID:
serverns `.env`.** Rollback: `b73d89c` för #36, `d35da53` för #35 (inga taggar hann sättas).

## Verifierat

Build 0 fel. Domain 276/276, Application 467/467, Client 23/23. E2E 226/227 på 19 min 18 s – enda
röda är `SkarmbilderTests`, som passerar isolerat. Produktion: RestartCount 0, health Healthy
(Europe/Stockholm), `scripts/Smoke` PASS på 390 och 1280 px, `.list-item-own-line` finns i utrullad
`app.css` (alltså nya bygget), och för #35 läser svepets SQL `r."Visibility"`.

## Öppna frågor

**E2E-flakigheten är belastningsberoende.** Samma gren gav 4 röda på 22 min 51 s när sviten delade
maskin med annat arbete, och 1 rött på 19 min 18 s när den fick köra ensam. Bara ett av de fyra
namnen loggades (`Adding_travel_minutes...`); det passerade isolerat. Kör sviten ensam före en
release. **Dev-databasen hade dessutom vuxit till 18 967 hushåll och 78 MB** –
E2E städar aldrig efter sig; den tömdes 2026-09-24 och fixturstädning är värd en egen uppgift.
Städa även gamla worktrees (`agent-a2836895c322add0f` ligger kvar). `Reconnected`-closuren fångar
`householdId`; byter någon hushåll rejoinas det gamla. Kvar sedan tidigare: etiketten "Tid i förväg".
