# Överlämning

Lägesbild per 2026-09-04, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`
(pushad, ej PR/merge). Utöver tidsbudget per medlem: `RecurrenceRule`, roterande ansvar
(`TaskAssignment`/`RotationPicker`), `MemberPreference`, och riktig realtidssynk via
SignalR (`HouseholdHub`) - se [ARCHITECTURE.md](ARCHITECTURE.md) §3/§5 för besluten. Klienten
har fått `Uppgifter`, `Områden` och `Hushåll` som riktiga sidor. 163 tester gröna (Domain 66,
Application 75, E2E 22 - alla mot en riktig webbläsare, inklusive en som bevisar
realtidspushen utan `page.ReloadAsync()`).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
Nyckeln `Jwt:SigningKey` ligger i user secrets, aldrig i repot.

**Denna maskin har PostgreSQL på port `5433`, inte `5432`** (något annat upptar 5432 lokalt).
`.env` sätter `POSTGRES_HOST_PORT=5433`; API:t behöver då
`ConnectionStrings__Hemordna=Host=localhost;Port=5433;...` i miljön. E2E-fixturen
(`HemordnaAppFixture`) känner inte till detta - startar den sin egen API-process hamnar den
mot fel port och timar ut efter 90s. Starta API+klient manuellt med rätt env **innan** E2E
körs, så återanvänder fixturen dem (se dess egen kommentar om detta beteende).

## Kända brister

- `Layout/NavMenu.razor`: `Planering` och `Inställningar` saknar fortfarande `@page`-rutt
  (medvetet uppskjutna skärmar - veckodiagram respektive visningsinställningar).
- PWA:n är verifierad i publiceringskedjan, inte i en riktig browser.
- `.vscode/launch.json`/`tasks.json` för F5-start finns fortfarande inte.

**Rör inte** `BlazorWebAssemblyLoadAllGlobalizationData` i `Hemordna.Client.csproj` - se
`MinDagTests.The_date_is_Swedish_even_when_the_browser_is_English` för varför.

## Öppna frågor och nästa steg

Endast offline-strategi kvarstår som `OPEN` i ARCHITECTURE.md §10.

1. Veckodiagram och "Senaste händelser"-flöde i Hushållsöversikten (medvetet uppskjutna).
2. En `Inställningar`-sida för `MemberPreference` (API:t finns redan, ingen UI).
3. Flera hushåll per användare - additivt, se ARCHITECTURE.md §4.
4. `.vscode/launch.json` och `tasks.json` så att API och klient startar med F5.
