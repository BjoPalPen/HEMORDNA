# Överlämning

Lägesbild per 2026-09-23. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` pushad, kodrelease `809c03e` deployad till https://app.hemordna.se. Sex PR (#23–#28)
sedan `21410b4`, fyra med datamigrering.

- **Pushnotiser för påminnelser** (PR #28). Två per påminnelse: "dags att gå", räknad bakåt från
  restiden, och en vid tiden. Ingen notis för en heldagspåminnelse eller en avbokad. Kedjan är
  portad från BowlingPlatform (samma stack, kör mot iPhone); `NotificationPolicy`-maskineriet
  därifrån följde medvetet INTE med. Urvalet är en ren funktion utan klocka eller databas, så
  sommartiden går att testa – testet binder även vad en naiv fast UTC+1 skulle räknat ut.
  **Markering "skickad" sker EFTER utskicket** (Björns beslut): hellre en dubblerad notis än en
  utebliven, eftersom en utebliven "dags att gå" betyder att man kommer för sent. Två tester
  binder ordningen – städa inte tillbaka den. Loop 5 min, fönster/cutoff 15 min.
- **Restid på påminnelser** (PR #27). `TravelMinutes` kräver ett klockslag, och ett borttaget
  klockslag nollar restiden. Raden visar avgångstiden ("Gå 13:30"), inte råa minuter.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`.
Docker Desktop krävs för lokal API/E2E – utan den faller ALLA E2E på "did not become
reachable", vilket ser ut som en regression men är tom miljö.
Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy:
`git pull --ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d
--build --no-deps hemordna-api` – migrationer körs vid uppstart, kontrollera
`docker logs hemordna-api` efter en schemaändring. Postgres heter `hemordna-postgres-1`.
En allow-regel för deploy-ssh i `settings.json` går före auto-lägets spärr. **VAPID-nycklarna
ligger i serverns `.env`**, aldrig i repot. Compose kräver dem med `:?` – saknas de vägrar
containern starta. Byt dem aldrig: alla befintliga prenumerationer slutar då fungera samtidigt.
`VAPID_SUBJECT` är just nu Björns gmail. `/health` svarar JSON med zonen.

## Verifierat

Build 0 fel, 0 varningar. Domän 255/255, Application 428/428, E2E 217/218 – det enda röda är
`SkarmbilderTests`, en belastningsflake som ger 4/4 isolerat. VAPID-paret bevisat giltigt genom
en riktig signerad JWT, och offline-cachen verifierad genom att faktiskt gå offline i Playwright.
Produktion: båda migreringarna applicerade, bakgrundstjänsten observerad starta, smoke PASS.

## Drift och kvarstående frågor

**Ingen har sett notiserna på en riktig iPhone.** Det kräver att appen ligger på hemskärmen – i
en Safari-flik kommer inga notiser alls. Enda obevisade delen av kedjan. Rollback-taggar:
`rollback-before-725fedc`, `-4ec6c59`, `-restid`, `-push`. `HouseholdClock`s fallback-gren är
inte enhetstestad; hälsokontroll och `Critical`-logg är mitigeringen. Två samtidiga containrar
kan racea på markeringen (unikt index fångar det) – inte härdat, driften är en container. Kvar
sedan tidigare: "Tid i förväg"-etiketten och en gles Heavy-uppgift.
