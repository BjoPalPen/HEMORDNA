# Överlämning

Lägesbild per 2026-09-24. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` pushad, kodrelease `4f0f3aa` deployad till https://app.hemordna.se. Åtta PR (#23–#30)
sedan `21410b4`, fyra med datamigrering.

- **Pushnotiser för påminnelser** (PR #28). Två per påminnelse: "dags att gå" (bakåt från restiden)
  och en vid tiden, bara för en `Upcoming` med klockslag. Loop 5 min, fönster 15 min. **Markering
  "skickad" sker EFTER utskicket** (Björns beslut, två tester binder det – städa inte tillbaka):
  hellre en dubblerad notis än en utebliven.
- **Bocka av en påminnelse** (PR #29). `ReminderStatus.CheckedOff` – namnet är handlingen
  medlemmen utförde, det enda appen vet. Två gränser, båda testade: den räknas ALDRIG som
  hushållsarbete (annars dras städschemat ner för ett tandläkarbesök), och den tystar sina notiser.
- **Gallring efter 30 dagar** (PR #30). En påminnelse raderas 30 dagar efter sitt EGET datum –
  dataminimering (§10), inte diskutrymme. Alla statusar lika; `SentReminderNotifications` följer
  med via FK-kaskad (verifierad i prod-databasen). Egen bakgrundstjänst, 24 h, loggar bara antal.
  `ResetHousehold` rör medvetet INTE påminnelser – privat tid är inte hushållsarbete.
- **Restid** (PR #27). `TravelMinutes` kräver klockslag; tas klockslaget bort nollas restiden.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker
Desktop krävs för lokal API/E2E – utan den faller ALLA E2E på "did not become reachable", vilket
ser ut som en regression men är tom miljö.
Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy:
`git pull --ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d
--build --no-deps hemordna-api` – migrationer körs vid uppstart. Postgres heter
`hemordna-postgres-1`. En allow-regel för deploy-ssh i `settings.json` går före auto-lägets
spärr. **VAPID-nycklarna ligger i serverns `.env`**, aldrig i repot; compose kräver dem med `:?`.
Byt dem aldrig: alla prenumerationer slutar då fungera samtidigt.

## Verifierat

Build 0 fel, 0 varningar. Domän 266/266, Application 440/440, E2E 218/219. Produktion:
RestartCount 0, båda bakgrundstjänsterna observerade starta, health Europe/Stockholm, smoke PASS.
`SkarmbilderTests` är **äkta flakigt, inte belastningsberoende** – rättelse av en tidigare
bedömning: det föll även isolerat (3/4) och passerade sedan isolerat (4/4). Förtjänar utredning.

## Drift och kvarstående frågor

**Ingen har sett notiserna på en riktig iPhone.** Kräver att appen ligger på hemskärmen – i en
Safari-flik kommer inga notiser alls. Enda obevisade delen av kedjan. Gallringen har ännu inte
raderat något (äldsta påminnelsen i prod: 2026-09-23). Rollback-taggar: `rollback-before-725fedc`,
`-4ec6c59`, `-restid`, `-push`, `-bockaav`. `HouseholdClock`s fallback-gren är inte enhetstestad;
hälsokontroll och `Critical`-logg är mitigeringen. Två containrar kan racea på notismarkeringen
(unikt index fångar det) – inte härdat, driften är en. Kvar: "Tid i förväg" och en gles Heavy.
