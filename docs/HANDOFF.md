# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`
(pushad, ej PR/merge). Utöver tidsbudget per medlem: `RecurrenceRule`, roterande ansvar
(`TaskAssignment`/`RotationPicker`), `MemberPreference`, och riktig realtidssynk via
SignalR (`HouseholdHub`) - se [ARCHITECTURE.md](ARCHITECTURE.md) §3/§5 för besluten. Klienten
har alla fem sidor från `NavMenu` plus `Inställningar`. Hushållsöversikten visar nu även
"Senaste händelser" (senast avklarade uppgifter, byggt på befintlig `TaskOccurrence`-data,
ingen ny entitet) och uppdateras live. 166 tester gröna (Domain 66, Application 78, E2E 25 -
alla mot en riktig webbläsare).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
`.vscode/launch.json` + `tasks.json` finns för F5 (compound "API + Client").

**Denna maskin har PostgreSQL på port `5433`, inte `5432`** (något annat upptar 5432 lokalt).
`.env` sätter `POSTGRES_HOST_PORT=5433`; API:t behöver då
`ConnectionStrings__Hemordna=Host=localhost;Port=5433;...` i miljön. E2E-fixturen
(`HemordnaAppFixture`) känner inte till detta - startar den sin egen API-process hamnar den
mot fel port och timar ut efter 90s. Starta API+klient manuellt med rätt env **innan** E2E
körs, så återanvänder fixturen dem.

## Kända brister

- PWA:n är verifierad i publiceringskedjan, inte i en riktig browser.
- Hushållsöversiktens veckomatris (mockup) är inte byggd - bara "Senaste händelser".

**Rör inte** `BlazorWebAssemblyLoadAllGlobalizationData` i `Hemordna.Client.csproj` - se
`MinDagTests.The_date_is_Swedish_even_when_the_browser_is_English` för varför.

**EF Core-fälla:** `OrderBy` efter en property på en redan konstruerad `record` i samma
queryable-kedja kan ge `InvalidOperationException` vid körning, inte kompilering. Sortera och
`Take` som anonym typ i databasen, bygg posten i minnet efteråt. Se `RecentActivityQuery`.

## Öppna frågor och nästa steg

Endast offline-strategi kvarstår som `OPEN` i ARCHITECTURE.md §10.

1. Visuell design matchad mot UXUI-mockupen (färgpalett, ikoner) - medvetet uppskjutet,
   funktion prioriterades.

**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll. Flera hushåll per
användare ska **inte** byggas - se ARCHITECTURE.md §4.
