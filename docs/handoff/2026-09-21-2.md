# Överlämning

Lägesbild per 2026-09-21 (kväll). Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` är pushad och kodrelease `7ed77c2` deployad till https://app.hemordna.se.
Tio releaser totalt (PR #11–#20) sedan `4f913ce`, ingen schema- eller datamigration.
De två senaste idag:

- **Storstäd är nu ett tillägg på reguljär städning** (alt. B, klar – `storstad-som-tillagg`-
  artefaktens rekommendation genomförd). Rumsanspråket grupperar bara på `(AreaId, IsRoutine)`,
  inte längre `VisitKind` – ett rums lätta och tunga uppgifter är samma besök, samma dag, samma
  person. Etiketten "Storstäd" är borttagen helt (inte "Tungt" – bara "Städ").
  `WeeklyPlacementPlanner`s "tyngst först" är också borta, störst-i-minuter-först är hela regeln.
  Ork-taket bevisat opåverkat. Se ARCHITECTURE.md "Beslut: Storstäd som tillägg".
- **Mulberry-attribution synlig för användaren** – ett kort längst ned i Inställningar (källa +
  CC BY-SA-licens), fanns tidigare bara i `icons/tasks/NOTICE.txt`.

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

Build 0 fel/varningar. Application 349/349 (ett test bytt ut, ett borttaget, två nya –
se PR #20). `WeeklyPlanTests.Previewing_the_week...` grönt isolerat efter en stale
"Storstäd"-assertion rättades i samma PR. Produktion: HTTPS-health Healthy,
`hemordna-api` Up utan omstart, smoke PASS 390/1280 px.

## Drift och kvarstående frågor

Ingen rollback-tagg togs före dessa releaser – tagga `hemordna-hemordna-api:latest` som
`rollback-before-<sha>` innan nästa deploy.
Två kända, orelaterade E2E-flakes kvarstår outredda: `SkarmbilderTests.Capture_the_seven_
enkla_losningar_screens`, `EnergyTests.Choosing_a_level...` – bekräftat inget med
storstäd/VisitKind att göra.
Två öppna beslut kvar, som artefakter: etiketten "Tid i förväg" (uppskjuten på Vecka-sidan),
och om en gles (Interval > 1) Heavy-uppgift kan dra in för mycket tid i ett enda sammanslaget
besök – inget nytt problem, men inte särbehandlat.
