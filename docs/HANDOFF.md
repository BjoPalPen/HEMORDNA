# Överlämning

Lägesbild per 2026-09-09, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

**`main` är i produktion; `feat/ledig-dag` är INTE mergad än** (väntar på uttryckligt godkännande).
Branchen bygger "Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg" - se
ARCHITECTURE.md "Beslut: Kvarlämnat, ..." för varje delmoment (kvarlämnat stannar hos ägaren,
`MemberDayOff`/`MemberTimeCredit`, "Imorgon"-sektion + "Gör idag i stället", `DayOffSheet`,
`dot-off` på Veckas/Hushålls prickmatris, "Tid i förväg: N min" på Vecka). Alla Del A–G/C1–C9/
D/E/F1–F8-steg är kodade och committade; Domain/Application/full E2E gröna senast körda.

**Två riktiga fel hittades och fixades under klientverifiering** (se samma ARCHITECTURE.md-
avsnitt för detaljer): `SetMemberDayOff.BringAllForward` kastade när dagen som markerades ledig
var densamma som "idag" och redan hade en egen förekomst där; och att hämta dagens/morgondagens
plan SAMTIDIGT (`Task.WhenAll`) racade `EnsureOccurrencesGenerated` och kunde dubblera dagens
uppgift (fångat av ett befintligt, orelaterat test).

**Känd, ej fixad brist**: klienten löser "idag" via lokal tid, flertalet endpoints via UTC -
stämmer inte överens runt lokal midnatt i en positiv UTC-offset-tidszon. Bara `POST .../complete`
fick en klient-överstyrning (`{ today }`) den här gången - se ARCHITECTURE.md.

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**PostgreSQL på port `5433`, inte `5432`** (`.env`). **LAN-åtkomst:** binda med
`--urls "http://*:PORT"`, inte `0.0.0.0`. **Deploy:** `ssh -i ~/.ssh/hetzner_deploy
deploy@62.238.45.45`, `cd ~/hemordna && git pull && docker compose -f docker-compose.prod.yml
up -d --build` - migrationer körs automatiskt vid API-uppstart (`Program.cs`).

## Fällor som kostat tid

`::deep` måste stå FÖRE den del av en selektor utanför komponentens renderträd. Playwrights
`ClickAsync()` vägrar klicka `aria-disabled="true"` - `ClickAsync(new(){Force=true})`. En full
E2E-körning som startar strax efter lokal midnatt (positiv UTC-offset) kan visa spridda,
orelaterade fel - se "Känd, ej fixad brist" ovan; kör om efter att UTC hunnit ikapp.

## Kända brister

Medvetet ej fixat race: att välja en roll skickar två samtidiga PUT (roll + veckobudget). Två
namngivna E2E-fladdrare under parallell körning (`HushallTests.Changing_a_members_role_…`,
`HouseholdInviteTests.Joining_with_a_valid_code_…`) - kör isolerat innan en röd körning antas.

## Öppna frågor och nästa steg

`feat/ledig-dag`: en sista full E2E-körning väntar på att klientens/serverns "idag" ska stämma
överens igen (se ovan) innan merge. Merge kräver uttryckligt godkännande, inte givet än.
