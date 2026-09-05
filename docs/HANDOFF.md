# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`, med
öppen PR #9 mot `main` (ej mergad). Utöver tidsbudget per medlem: `RecurrenceRule`, roterande
ansvar, `MemberPreference`, riktig realtidssynk via SignalR - se
[ARCHITECTURE.md](ARCHITECTURE.md) §3/§5. Min dag/Planering saknar helt tidsval (§6a);
tid sätts bara via roll på Hushållsöversikten (§6b).

**Nytt: våningsguide och tidsfilter, ett medvetet undantag från "tid syns aldrig".** Områden
har nu en flerrumsguide - en valfri **våning** (fritext) kan fyllas med flera rumstyper och
**antal** i ett svep, i stället för att skapa varje rum för hand. Fler än ett av samma typ
numreras ("Sovrum 1", "Sovrum 2"); en våning blir prefix på namnet ("Våning 1 – Kök"). Direkt
efter skapandet visas en tidssammanfattning per rum (`RoomTemplate.TotalMinutes`) och en
totalsumma - se `Omraden.razor` `CreateFloorAsync`. Uppgifter har fått ett "Filtrera efter
område"-filter med samma sorts summering (`FilteredTasks`/`FilterSummaryText`). Se DESIGN.md
§6b: under planering är "hur lång tid tar rummet?" rimligt att svara på med en siffra, till
skillnad från den dagliga vyn. Rena klientfunktioner; ingen domän- eller API-ändring.
177 tester gröna (Domain 66, Application 78, E2E 33).

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

**Playwright-fälla:** `.list-item` på Områden måste skopas till `ul[aria-label='Områden']` -
"just skapat"-sammanfattningen renderar samma radtext. `GetByLabel` substräng-matchar, så
snarlika etiketter ("Område" / "Filtrera efter område") kräver `<form>`-scoping, inte `Exact`.

## Öppna frågor och nästa steg

Våningsguiden namnger med fritext/prefix, ingen egen `Floor`-modell - avvakta om det räcker.

**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll (ARCHITECTURE.md §4).
