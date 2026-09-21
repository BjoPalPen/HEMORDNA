# Överlämning

Lägesbild per 2026-09-21. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` är pushad och kodrelease `55690a6` deployad till https://app.hemordna.se.
Åtta releaser totalt (PR #11–#18) sedan `4f913ce`, ingen schema- eller datamigration.
De två senaste idag:

- **Rengör ugnen** (Kök, Tung) + **Byta sängkläder** (Sovrum, Mellan) i `RoomTemplates.cs`.
  Tider mot appens egna Tunga uppgifter, ej städbranschens (se `internetresearch`-artefakten).
  Kök har nu sin första Tunga uppgift → splittras i två besök av Planera veckan, se
  `storstad-som-tillagg`-artefakten (alt. B rekommenderas, ej byggt).
- **Dagliga rutiner slutar bygga på sig själva.** Routine (daglig, intervall 1) hade en
  slot per dag → N obockade dagar gav N permanenta kort. Nu skippas äldre missade dagar
  tyst; bara senaste stannar. Scoped till Interval 1, Weekly/Monthly oförändrat.
  `EnsureOccurrencesGenerated.GenerateOnScheduleAsync`.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`.
Docker Desktop måste köra för lokal API/E2E. LAN: `--urls "http://*:PORT"`.
Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`.
Deploy: `git pull --ff-only origin main`, sedan
`docker compose -f docker-compose.prod.yml up -d --build --no-deps hemordna-api`.
Production kräver Resend-nyckel. Betrott proxynät: `172.19.0.0/16`; verifiera vid nätbyte.
Browserkontroll: `dotnet run --project scripts/Smoke -- https://app.hemordna.se`.
Auto-läget har både blockerat och tillåtit ssh denna vecka – inkonsekvent, lämna auto
(Shift+Tab) om det nekas.

## Verifierat

Build 0 fel/varningar. Domän 188/188. Application 349/349 (346 + 3 nya för
rutinfixen). `OmradenTests` 19/19 efter mallilläggen (2 summor uppdaterade).
Produktion: HTTPS-health Healthy, `hemordna-api` Up utan omstart, smoke PASS 390/1280 px.

## Drift och kvarstående frågor

Ingen rollback-tagg togs före dessa releaser – tagga `hemordna-hemordna-api:latest` som
`rollback-before-<sha>` innan nästa deploy. Servern rapporterade "System restart
required" och 46 uppdateringar i går; ovverifierat idag.
Tre öppna beslut, alla som artefakter i sessionen: storstäd-som-tillägg (alt. B
rekommenderas), etiketten "Tid i förväg" (uppskjuten), Mulberry-attribution (bara i
NOTICE.txt, ej synlig för användaren).
