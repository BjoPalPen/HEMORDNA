# Överlämning

Lägesbild per 2026-09-19. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` är pushad och kodrelease `a92e12f` deployad till https://app.hemordna.se.
Tre releaser i samma deploy (PR #11–#13), ingen schema- eller datamigration:

- Glesa regler lämnas i fred: `RecurrenceRule.IsWeeklyRhythm` (Weekly/Monthly med
  intervall 1) grindar auto-placering, Planera veckan, "Använd" och veckodagslåsning.
  Regler med intervall > 1 ankras aldrig om och belastar inte veckokapaciteten.
  Nytt fält "Första gången" för glesa regler i båda formulären. Se ARCHITECTURE.md
  "Beslut: Glesa regler lämnas i fred". Regler som omankrats före releasen repareras inte.
- Bakgrundsbild per enhet (IndexedDB, nedskalad i webbläsaren, Lugnare skärm vinner).
- Uppgiftsikoner 24→32 px i listan och `--icon-contrast` 1,25 ljust / 1,15 mörkt.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`.
Docker Desktop måste köra för lokal API/E2E. LAN: `--urls "http://*:PORT"`.
Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`.
Deploy: `git pull --ff-only origin main`, sedan
`docker compose -f docker-compose.prod.yml up -d --build --no-deps hemordna-api`.
Production kräver Resend-nyckel. Betrott proxynät: `172.19.0.0/16`; verifiera vid nätbyte.
Browserkontroll: `dotnet run --project scripts/Smoke -- https://app.hemordna.se`.
Ny klient i produktion syns på `GET /js/backdrop.js` → 200 (saknas i äldre build).

## Verifierat

Slutlig build: 0 fel/varningar. Domän: 188/188. Application: 346/346.
Full E2E-körning på PR #11: 207/210. Två fel i TaskFrequencyTests var deterministiska
(intervallfältet band på change, inte input) och rättades i `985b202`; ett taltest var en
laddningstimeout som passerade isolerat. Riktad E2E efter rättning: 7/7.
PR #12: InstallningarTests 6/6, Speech 2/2, AlwaysOnWeekday 2/2. PR #13: MinDag 10/10.
Produktion: HTTPS-health Healthy, `hemordna-api` Up utan omstart, browserkontroll 390/1280 px.
Testresultat och skärmbilder ligger lokalt i gitignorerade `TestResults/`.

## Drift och kvarstående frågor

Ingen rollback-tagg togs före denna release – tagga `hemordna-hemordna-api:latest` som
`rollback-before-<sha>` innan nästa deploy. Återställ vid behov som i handoff/2026-09-18.md.
Servern rapporterar "System restart required" och 46 väntande paketuppdateringar.
Bakgrundens wash (`--backdrop-wash` 0,55/0,60) och ikonernas skärpa bedöms på riktig telefon;
nästa steg för ikonerna är i så fall SVG-filernas linjetjocklek, inte mer CSS.
Mulberry Symbols (CC BY-SA) kräver synlig attribution – finns idag bara i `icons/tasks/NOTICE.txt`.
Deploy från Claude Code kräver en Bash-regel för ssh till deploy-kontot i `settings.local.json`.
