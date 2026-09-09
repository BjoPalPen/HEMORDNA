# Överlämning

Lägesbild per 2026-09-09, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

**`main` kör i produktion** med "Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg" mergad
och deployad - se ARCHITECTURE.md "Beslut: Kvarlämnat, ..." för varje delmoment. Direkt därefter:
konsekvent klient-överstyrt "idag" (`fix/client-today-consistency`) - bring-forward, ledig dag och
tid i förväg tar nu alla en valfri klient-`today`, samma mönster `POST .../complete` redan hade
men aldrig fick inkopplat vid anropsstället. `rebalance-assignments`/`daily-summary` är medvetet
kvar på serverns UTC (hushållsomfattande, ska vara konsekvent för alla).

**Produktionsfeedback samma dag, INTE en bugg**: en användare rapporterade samma uppgiftsnamn två
gånger på Idag. Verifierat mot databasen - två genuint skilda `TaskOccurrence`-rader (en daglig
uppgifts gårdagsförekomst, ej avklarad, plus dagens nya), båda hos samma medlem. Exakt avsedd
konsekvens av "kvarlämnat stannar" - innan dess kunde en försenad förekomst tyst byta ägare vid
ombalansering, vilket dolde just den här hopningen. Öppen, ej byggd UX-idé: ge "Sedan
tidigare"-raderna ett tydligt datum så det inte läses som en dubblett.

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`, **Postgres
`5432`** (`docker-compose.yml` i repo-roten, `docker compose up -d db`). **Docker Desktop måste
köra lokalt** innan E2E-svit eller `dotnet run` mot databasen - annars misslyckas HELA svitens
`HemordnaAppFixture` med "did not become reachable" (Npgsql-fel i loggen avslöjar det verkliga
skälet). **LAN-åtkomst:** binda med `--urls "http://*:PORT"`, inte `0.0.0.0`. **Deploy:** `ssh -i
~/.ssh/hetzner_deploy deploy@62.238.45.45`, `cd ~/hemordna && git pull && docker compose -f
docker-compose.prod.yml up -d --build` - migrationer körs automatiskt vid API-uppstart.

## Fällor som kostat tid

`::deep` måste stå FÖRE den del av en selektor utanför komponentens renderträd. Playwrights
`ClickAsync()` vägrar klicka `aria-disabled="true"` - `ClickAsync(new(){Force=true})`. En full
E2E-körning som startar strax efter lokal midnatt (positiv UTC-offset) kunde tidigare visa
spridda, orelaterade fel - löst för de flesta medlemsinitierade endpoints, se ovan.

## Kända brister

Medvetet ej fixat race: att välja en roll skickar två samtidiga PUT (roll + veckobudget). Två
namngivna E2E-fladdrare under parallell körning (`HushallTests.Changing_a_members_role_…`,
`HouseholdInviteTests.Joining_with_a_valid_code_…`) - kör isolerat innan en röd körning antas.

## Öppna frågor och nästa steg

Inga öppna frågor eller väntande godkännanden just nu utöver att merga/deploya
`fix/client-today-consistency`.
