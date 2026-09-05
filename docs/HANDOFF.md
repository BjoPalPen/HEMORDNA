# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`, med
öppen PR #9 mot `main` (ej mergad). Utöver tidsbudget per medlem: `RecurrenceRule`, roterande
ansvar, `MemberPreference`, riktig realtidssynk via SignalR - se
[ARCHITECTURE.md](ARCHITECTURE.md) §3/§5.

**Tid är helt dold i gränssnittet** (produktfeedback: talvisning skapar stress). Domänen
räknar fortfarande i minuter internt; klienten mappar till fyra kvalitativa lägen
(`Support.TimeLevel`) och visar aldrig en siffra - se DESIGN.md §6a.

**Ännu färre val vid start** (uppföljande feedback: fyra lägen per veckodag var för mycket).
`Support.HouseholdRolePresets` ger tre roller (Vuxen heltid/Barn-ungdom/Pensionär) som i ett
klick sätter hela veckans budget, vid "Lägg till medlem" och i Min dags "Din vanliga vecka";
dag-för-dag-redigeringen ligger kvar bakom "Anpassa varje dag för sig". `Support.RoomTemplates`
ger en färdig, roterande veckouppgiftslista när ett rum namnges efter typ (Litet wc/Badrum/
Kök/Sovrum/Vardagsrum/Hall) på Områden - se DESIGN.md §6b. Båda rena klientfunktioner ovanpå
befintliga API-anrop; ingen domän- eller API-ändring.
176 tester gröna (Domain 66, Application 78, E2E 32 - alla mot en riktig webbläsare).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**Denna maskin har PostgreSQL på port `5433`, inte `5432`** (`.env` sätter
`POSTGRES_HOST_PORT=5433`).

**LAN-åtkomst:** binda med `--urls "http://*:PORT"`, inte `"http://0.0.0.0:PORT"` - `0.0.0.0`
öppnar bara IPv4, medan `*` binder dual-stack. Krävs även för `localhost`/E2E-fixturen, som
kan slå upp `::1` först på denna maskin. Windows brandvägg hade Block-regler för
`Hemordna.Api.exe` på "Public"; kräver admin att ändra (`Get-NetFirewallRule`).

## Kända brister

- PWA:n är verifierad i publiceringskedjan, inte i en riktig browser.
- Ikoner är text/symboler, inte den riktiga ikonuppsättningen från mockupen.

**EF Core-fälla:** `OrderBy` på en redan konstruerad `record` i samma queryable-kedja kan ge
`InvalidOperationException` vid körning. Sortera som anonym typ, mappa i minnet. Se
`RecentActivityQuery`.

## Öppna frågor och nästa steg

Rumsmallarna täcker rumstyp, inte våningsantal (ursprunglig produktidé nämnde "sovrum per
våning") - vänta på användarens utvärdering innan det byggs vidare.

**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll (ARCHITECTURE.md §4).
