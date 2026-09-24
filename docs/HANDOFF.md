# Överlämning

Lägesbild per 2026-09-24. Arbetssätt: [../CLAUDE.md](../CLAUDE.md).
Äldre lägesbilder bevaras i [handoff/](handoff/). Max 50 rader.

## Läge

`main` pushad, kodrelease `2636c94` deployad till https://app.hemordna.se. Elva PR (#23–#33)
sedan `21410b4`, fem med datamigrering.

- **Påminnelser** – restid, två pushnotiser, avbockning, gallring efter 30 dagar (PR #27–#30).
  Notiserna är sedda på en riktig iPhone. Avbockning räknas ALDRIG som hushållsarbete.
- **Notis efter ändrad tid** (PR #31). Registret nycklas nu på `(ReminderId, Kind, ScheduledFor)`;
  förut blev en flyttad påminnelse tyst för alltid. `MoveReminder` m.fl. är medvetet orörda – de
  ska inte veta något om push. Tidpunkten normaliseras till hela minuter (exakt likhet mot µs).
- **Nedräkning mot avgång** (PR #32). Staplar, en per 5 min, inom 60 min, bara närmaste
  påminnelsen. **Kvantumet är fast** – takten är informationen, sträck aldrig ut dem. Nytt
  projekt `Hemordna.Client.Tests`, skälet står i CLAUDE.md §8.
- **Realtid blockerar inte längre sidan** (PR #33). `ConnectAsync` kastade, `OnInitializedAsync`
  fångade inte, och den döda anslutningen låg kvar – sidan frös permanent på "Hämtar din dag…".
  `Speech` hade samma fel. Båda felvägarna saknar seam och är medvetet otestade.

## Köra och deploya

Uppstart: [../README.md](../README.md). API `5199`, klient `5200`, Postgres `5432`. Docker
Desktop krävs för lokal API/E2E – annars faller ALLA E2E på "did not become reachable". Server: `ssh -i ~/.ssh/hetzner_deploy deploy@62.238.45.45`, checkout `~/hemordna`. Deploy:
`git pull --ff-only origin main`, sedan `docker compose -f docker-compose.prod.yml up -d
--build --no-deps hemordna-api` – migrationer körs vid uppstart. Postgres heter
`hemordna-postgres-1`. En allow-regel för deploy-ssh i `settings.json` går före auto-lägets
spärr. **VAPID-nycklarna ligger i serverns `.env`**, aldrig i repot. Byt dem aldrig: alla
prenumerationer slutar då fungera samtidigt.

## Verifierat

Build 0 fel, 0 varningar. Domain 267/267, Application 446/446, Client 13/13. Produktion:
RestartCount 0, trekolumnsindexet på plats i databasen, båda bakgrundstjänsterna observerade
starta, health Europe/Stockholm, smoke PASS.

## Öppna frågor

**E2E faller slumpmässigt på sidladdning** – 1–7 röda per körning, olika tester varje gång, alla
med `Hämtar din dag…` i ariasnapshotten. **Rättelse:** bedömningen att `SkarmbilderTests` är
"äkta flakigt, oberoende av belastning" är tillbakadragen, den var aldrig belagd. PR #33 är inte
heller bevisad som orsak – den körning som skulle visa det stördes av mätningar jag själv körde
samtidigt. Dev-maskinen låg på 82 % CPU och 81 % RAM. **Nästa steg: fånga webbläsarkonsolen i
E2E-fixturen** – i dag går "sidan kraschade" inte att skilja från "sidan var långsam".
`Reconnected`-closuren fångar `householdId`; byter någon hushåll rejoinas det gamla.
14 worktrees (3,3 GB) ligger kvar under `.claude/`, elva av dem döda. Gallringen har ännu inte
raderat något – äldsta påminnelsen i prod är 2026-09-23. Rollback-taggar:
`rollback-before-725fedc`, `-4ec6c59`, `-restid`, `-push`, `-bockaav`. Kvar: "Tid i förväg".
