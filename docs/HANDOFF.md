# Överlämning

Läge per 2026-09-26. Arbetssätt: [../CLAUDE.md](../CLAUDE.md). Äldre bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` = `0b08cf4`, deployad. Senast: PR #39, refresh-token med rotation.

- **Refresh-token med rotation** (PR #39). Access-token 30 min, **enbart i minnet** – skrivs aldrig till disk.
  Refresh-token 60 dagar i `localStorage` under `hemordna.refresh`, ny livslängd vid varje rotation. Endast
  en SHA-256-hash lagras. **Återanvändning av en förbrukad token återkallar hela kedjan**, inklusive den
  token som hann utfärdas ur den. **LösenordsBYTE** behåller enheten man står vid och dödar övriga;
  **återställning** dödar allt (den används vid misstänkt intrång). **Single-flight-spärren i klienten är
  inte valfri:** utan den blir samtidiga 401 till samtidiga `/refresh` med samma token, vilket tolkas som
  stöld – funktionen hade då loggat ut folk OFTARE än före ändringen.
- **`AsNoTracking()` i `RefreshTokenRepository.FindByHashAsync` är ett korrekthetskrav**, inte en
  läsoptimering. Utan den ser omläsningen aldrig en kapplöpande återkallning, eftersom `ExecuteUpdate` inte
  rör change trackern. Detta klarade SAMTLIGA enhetstester och föll först mot riktig Postgres i
  `Hemordna.E2E.Tests.RefreshTokenTests` – **det testet får aldrig tas bort.**
- **Påminnelser** (#35/#36/#38): `Visibility` styr VAD (`Private`/`BusyOnly`/`Household`), `ReminderAudience`
  styr VEM (`Everyone`/`Selected`). Platsen delas aldrig. Mottagaren kan ta en delad tid som egen, fristående
  kopia. Fail-closed hela vägen. **Steg 3 (medlem utan konto) är prövat och AVSLAGET** – bygg det inte.
  Allt: `Beslut: Synlighet för påminnelser` och `Beslut: Refresh-token med rotation` i
  [ARCHITECTURE.md](ARCHITECTURE.md).

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker Desktop krävs lokalt
– annars faller ALLA E2E på "did not become reachable". Server: `ssh -i ~/.ssh/hetzner_deploy
deploy@62.238.45.45`, checkout `~/hemordna`. Deploy: `git pull --ff-only origin main`, sedan `docker compose -f
docker-compose.prod.yml up -d --build --no-deps hemordna-api`; migrationer körs vid uppstart, Postgres heter
`hemordna-postgres-1`. **VAPID: serverns `.env`.** Rollback: `7c54c48` (#39), `4ff2ea0` (#38), `6f454cc` (#37).

## Verifierat

Build 0 fel. Domain 309/309, Application 513/513, Client 26/26, Api 7/7. E2E 237/239 ensam på maskinen – de två
röda är kända `SkarmbilderTests` och ett påminnelsetest vars körning passerade midnatt (`DateTime.Now` +
`AppDate.Today`), båda gröna isolerat. Produktion: RestartCount 0, health Healthy, `scripts/Smoke` PASS,
`Applying migration '…AddRefreshTokens'` i loggen, och `POST /api/auth/refresh` svarar 401 på ogiltig token.

## Öppna frågor

**Alla användare loggades ut EN gång vid #39:s driftsättning** – väntat, ingen hade en refresh-token än.
Återkommer det är det ett fel. **E2E-flakigheten är belastningsberoende** (fyra mätpunkter): kör sviten ensam
före release, och se om `GuideRenderTests` återkommer – den sågs röd en gång under en delad körning.
**Dev-databasen** växer obegränsat (E2E städar aldrig); tömd 2026-09-24. Fixturstädning och den kvarliggande
låsta worktreen är värda egna uppgifter. Kvar sedan tidigare: `Reconnected`-closuren fångar `householdId`, och
etiketten "Tid i förväg" (medvetet orörd). **Uppskjutet härdningssteg:** refresh-token i HttpOnly-cookie i
stället för `localStorage` – möjligt i prod (samma origin), men dev och E2E är cross-origin.
