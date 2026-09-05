# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`, med
öppen PR #9 mot `main` (ej mergad). Utöver tidsbudget per medlem: `RecurrenceRule`, roterande
ansvar, `MemberPreference`, riktig realtidssynk via SignalR - se
[ARCHITECTURE.md](ARCHITECTURE.md) §3/§5.

**Min dag har inget tidsval längre.** "Snabbval" (dagens tillfälliga avvikelse) är borttaget
helt - feedback: även det lilla, kvalitativa valet kändes rörigt och stressande på sidan alla
öppnar varje dag. Samma "Idag"-kort togs bort från Planering av samma skäl; den sidan är nu
ren läsning av veckans roll-satta nivåer. `availableMinutes` finns kvar som API-kapacitet
(testad i `DatumValideringTests`) men har ingen knapp kvar i gränssnittet.

Tid sätts nu bara via roll (`Support.HouseholdRolePresets`: Vuxen heltid/Barn-ungdom/
Pensionär) från Hushållsöversiktens medlemslista - inte på Min dag, se DESIGN.md §6a/§6b.
`Support.RoomTemplates` ger städ-checklistor per rumstyp på Områden. Allt rena
klientfunktioner ovanpå befintliga API-anrop; ingen domän- eller API-ändring.
174 tester gröna (Domain 66, Application 78, E2E 30).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**Denna maskin har PostgreSQL på port `5433`, inte `5432`** (`.env` sätter
`POSTGRES_HOST_PORT=5433`).

**LAN-åtkomst:** binda med `--urls "http://*:PORT"`, inte `"http://0.0.0.0:PORT"` - `0.0.0.0`
öppnar bara IPv4, medan `*` binder dual-stack. Krävs även för `localhost`/E2E-fixturen, som
kan slå upp `::1` först på denna maskin.

## Kända brister

PWA:n är overifierad i en riktig browser; ikoner är text/symboler, inte mockupens.
`HushallTests.Changing_a_members_role_...` har visat sig flaky under full parallell körning
(passerar isolerat och i omkörning) - misstänkt timing, inte en produktbugg. Ej åtgärdad.

**EF Core-fälla:** `OrderBy` på en redan konstruerad `record` i samma queryable-kedja kan ge
`InvalidOperationException` vid körning. Sortera som anonym typ, mappa i minnet.

## Öppna frågor och nästa steg

Rumsmallarna täcker rumstyp, inte våningsantal (ursprunglig idé: "sovrum per våning") - vänta
på användarens utvärdering innan det byggs vidare.

**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll (ARCHITECTURE.md §4).
