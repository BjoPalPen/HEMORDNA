# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`, med
öppen PR #9 mot `main` (ej mergad).

**Sovrumsmallen har en riktig städrytm** i stället för en enda uppskattning: bädda sängen +
vädra dagligen, dammsug två gånger i veckan (`TaskFrequency.TwiceWeekly` - byggs som daglig
regel med 3 dagars intervall, ingen kalenderplats för "2x/vecka" annars - ARCHITECTURE.md §3),
torka golvet varje vecka, torka lister/tvätta fönster en gång i månaden.

**Roll är nu en sparad egenskap** på `HouseholdMember` (`Role`), inte längre bara gissad från
budgeten. `TaskDefinition.RequiresAdult` låter en mallad uppgift (sovrummets "Tvätta fönster")
hoppas över av barn i rotationen - `RotationPicker` faller tillbaka till hela hushållet om
ingen vuxen är kvar. Ny migration `AddMemberRoleAndTaskRequiresAdult`, applicerad lokalt.

**"Lägg till vanliga hushållssysslor"** (`GeneralTaskTemplates`, Övrigt-kortet) fanns redan
från förra passet; nu dokumenterad i DESIGN.md §6b.

**Playwright-lärdomar (nya):** (1) klientens lokala, gitignorade
`appsettings.Development.json` kan peka på LAN-IP:n (för mobiltest) i stället för `localhost` -
starta då dev-API:t med `--urls "http://*:5199"`, annars svarar det bara på egen bindningsadress.
(2) Två oberoende `PUT`-anrop i sekvens fördubblar rundresetiden i onödan och gör ett
tajmningskänsligt test flakigt - kör dem med `Task.WhenAll` (se `Hushall.razor`s rollväljare).

197 tester gröna (Domain 69, Application 90, E2E 38).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**PostgreSQL på port `5433`, inte `5432`** (`.env`). Migrationer: `dotnet ef database update`
med samma `ConnectionStrings__Hemordna` som API:t.

**LAN-åtkomst:** binda med `--urls "http://*:PORT"`, inte `0.0.0.0` - det öppnar bara IPv4.

## Kända brister

PWA:n är overifierad i riktig browser. `HushallTests.Changing_a_members_role_...` var flaky
under full parallell körning - trolig orsak (sekventiella anrop, se ovan) åtgärdad, men inte
omtestad under lång tid ännu.

## Öppna frågor och nästa steg

Väckt men **inte påbörjad**: uppskatta städbehov utifrån antal rum/medlemmar/husdjur.
**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll (ARCHITECTURE.md §4).
