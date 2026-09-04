# Överlämning

Lägesbild per 2026-09-04, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är aktuell. Inga öppna PR:er, inga omergade brancher. 119 tester gröna, build utan
varningar. Domän, planering, EF Core mot PostgreSQL, Minimal APIs med JWT och
`HouseholdAccessFilter`, samt Blazor WASM med `LoggaIn` och `MinDag` – installerbar som PWA,
svensk kultur. Per-beslut-status finns i [ARCHITECTURE.md](ARCHITECTURE.md), märkt
`IMPLEMENTED`, `PROPOSED` eller `OPEN`.

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`,
PostgreSQL `5432`. Nyckeln `Jwt:SigningKey` ligger i user secrets, aldrig i repot.

Två fällor som kostat tid:

- **Codespace kontra lokalt.** Repot har körts i båda parallellt. `pwd` som ger
  `/workspaces/...` betyder Codespace, `C:\...` eller `E:\...` betyder lokalt.
- **`--no-launch-profile` ger Production**, user secrets läses inte, och API:t dör med
  `'Jwt:SigningKey' is not configured`. Kör `dotnet run` utan flaggan.

## Kända brister

- Fyra av fem länkar i `Layout/NavMenu.razor` saknar `@page`-rutt. Bara `/` och `/logga-in`
  finns; övriga ger Blazors "nothing at this address".
- E2E-suiten är flaky vid parallellkörning i devcontainern – Chromium kraschar. Kör med
  `-- xUnit.ParallelizeTestCollections=false`.
- PWA:n är verifierad i publiceringskedjan, inte i en riktig browser. Att Chrome erbjuder
  installation och att appen startar utan nätverk är oprövat.

**Rör inte** `BlazorWebAssemblyLoadAllGlobalizationData` i `Hemordna.Client.csproj` för att
spara nedladdning. Utan den väljer Blazor ICU-data efter browserns språk, en engelsk browser
får en shard utan svenska, och datumen blir engelska igen. Att sätta kulturen i `Program.cs`
räcker inte ensamt – båda behövs. Fångas av
`MinDagTests.The_date_is_Swedish_even_when_the_browser_is_English`.

## Öppna frågor och nästa steg

Från [ARCHITECTURE.md](ARCHITECTURE.md) avsnitt 10: vem som genererar `TaskOccurrence`, hur
roterande ansvar räknas ut, offline-strategi bortom read-only cache. Auth-scoping är på plats
och upprätthålls; det som saknas är flera hushåll per användare, vilket är additivt.

1. `RecurrenceRule`, som låser upp occurrence-genereringen och två av de öppna frågorna.
2. SignalR-hub per hushåll, gruppen `household:{id}`, för realtidssynk.
3. `.vscode/launch.json` och `tasks.json` så att API och klient startar med F5.
