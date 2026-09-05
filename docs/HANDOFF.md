# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`, med
öppen PR #9 mot `main` (ej mergad). Utöver tidsbudget per medlem: `RecurrenceRule`, roterande
ansvar, `MemberPreference`, riktig realtidssynk via SignalR - se
[ARCHITECTURE.md](ARCHITECTURE.md) §3/§5.

**Tid är helt dold i gränssnittet**, satt via tre roller i stället för minuter eller dagliga
val - `Support.HouseholdRolePresets` (Vuxen heltid/Barn-ungdom/Pensionär), se DESIGN.md
§6a/§6b. `Support.RoomTemplates` ger en roterande veckouppgiftslista per rumstyp på Områden.
Båda rena klientfunktioner ovanpå befintliga API-anrop.

**Roll flyttad från Min dag till Hushållsöversikten** (feedback: att välja/ändra roll är
"tid som ett val", inte alla medlemmar behöver se det dagligen). Min dag har bara kvar
**Snabbval**. Varje medlem i hushållslistan har en egen rollväljare
(`RoleValueFor`/`SetMemberRoleAsync`, `Hushall.razor`; `HouseholdRolePresets.Match` visar
"Anpassad tid" annars). Dag-för-dag-redigeringen är borttagen helt.
175 tester gröna (Domain 66, Application 78, E2E 31).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**Denna maskin har PostgreSQL på port `5433`, inte `5432`** (`.env` sätter
`POSTGRES_HOST_PORT=5433`).

**LAN-åtkomst:** binda med `--urls "http://*:PORT"`, inte `"http://0.0.0.0:PORT"` - `0.0.0.0`
öppnar bara IPv4, medan `*` binder dual-stack. Krävs även för `localhost`/E2E-fixturen, som
kan slå upp `::1` först på denna maskin. Windows brandvägg hade Block-regler för
`Hemordna.Api.exe` på "Public"; kräver admin att ändra (`Get-NetFirewallRule`).

## Kända brister

PWA:n är overifierad i en riktig browser; ikoner är text/symboler, inte mockupens.

**EF Core-fälla:** `OrderBy` på en redan konstruerad `record` i samma queryable-kedja kan ge
`InvalidOperationException` vid körning. Sortera som anonym typ, mappa i minnet. Se
`RecentActivityQuery`.

## Öppna frågor och nästa steg

Rumsmallarna täcker rumstyp, inte våningsantal (ursprunglig idé: "sovrum per våning") - vänta
på användarens utvärdering innan det byggs vidare.

**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll (ARCHITECTURE.md §4).
