# Hemordna – Arkitektur

Status per 2026-09-04. Varje avsnitt är märkt:

| Etikett | Betydelse |
|---|---|
| `IMPLEMENTED` | finns i koden och är verifierat med build/test |
| `PROPOSED` | beslutat inriktning, ännu inte byggt |
| `OPEN` | beslut som ännu inte är fattat |

Produktkraven finns i [PRODUCT.md](PRODUCT.md). Arbetsregler i [../CLAUDE.md](../CLAUDE.md).

---

## 1. Solutionstruktur — `IMPLEMENTED`

```text
Hemordna.slnx

src/
  Hemordna.Domain          entiteter, value objects, domänregler
  Hemordna.Application     use cases, DailyPlanner, resultatmodeller
  Hemordna.Infrastructure  EF Core, PostgreSQL, persistence
  Hemordna.Api             Minimal APIs, DTO:er, auth boundary
  Hemordna.Client          Blazor WebAssembly PWA

tests/
  Hemordna.Domain.Tests
  Hemordna.Application.Tests

docs/
```

Alla projekt är `net10.0` med `Nullable` och `ImplicitUsings` påslaget.
Solutionformatet är `.slnx` och ska inte konverteras till `.sln`.

Projektmall-koden (weatherforecast, Counter, Weather) är borttagen. `Hemordna.Client`
innehåller ännu bara ett skal - "Min dag" byggs härnäst.

---

## 2. Lager och dependency direction — `IMPLEMENTED`

```text
Domain  ←  Application  ←  Infrastructure  ←  Api
                                              ↑ HTTP
                                           Client
```

Beroenden går bara åt ett håll.

- **Domain** har noll paketreferenser. Inga EF Core-, ASP.NET Core-, Npgsql-, SignalR- eller
  Blazor-beroenden – det är villkoret för att domänreglerna ska gå att testa utan miljö.
- **Application** refererar Domain. Den känner inte till HTTP eller EF Core.
- **Infrastructure** refererar Application och Domain och äger all persistence.
- **Api** refererar Application och Infrastructure, och innehåller ingen affärslogik.
- **Client** pratar HTTP mot Api och implementerar inga affärsregler.

---

## 3. Datamodell — `IMPLEMENTED`

```text
Household ──┬── HouseholdMember ── WeeklyTimeBudget (value object)
            │         │
            │         └── MemberAvailability   (engångsundantag per datum)
            └── Area

TaskDefinition ──ScheduleFor──► TaskOccurrence
```

| Typ | Roll |
|---|---|
| `Household` | Hushållet. Tenant- och säkerhetsgräns. Äger `Members` och `Areas`. |
| `HouseholdMember` | Person i hushållet. Deaktiveras, raderas inte. |
| `WeeklyTimeBudget` | Immutable value object: normala minuter per veckodag. |
| `MemberAvailability` | Undantag för *ett* datum. "Mindre tid idag" utan att veckan ändras. |
| `Area` | Del av hemmet, eller annan gruppering hushållet väljer ("Hund"). |
| `TaskDefinition` | Hushållets stående beskrivning av ett arbete – normen. |
| `TaskOccurrence` | En konkret instans på ett datum. Här sker allt tillfälligt. |
| `TaskPriority` | `Low` / `Normal` / `High`, ordnad så att högre värde = högre prioritet. |
| `TaskOccurrenceStatus` | `Planned` / `Completed` / `Skipped`. |

### Definition kontra occurrence

Detta är modellens viktigaste gräns. `TaskDefinition` ändras aldrig för att uttrycka något
tillfälligt – att någon är bortrest eller att något hoppas över en gång hanteras på
`TaskOccurrence`.

`TaskOccurrence` **snapshottar** `EstimatedMinutes`, `Priority` och `CanBeDeferred` från
definitionen vid schemaläggning. En senare redigering av definitionen får inte i efterhand
skriva om arbete som redan ligger på någons dag. Verifierat av
`TaskDefinitionTests.Editing_the_definition_does_not_rewrite_an_already_scheduled_occurrence`.

### Beslut: uppskjutning är inte en status

`Deferred` finns medvetet inte i `TaskOccurrenceStatus`. Att skjuta upp flyttar
`ScheduledDate` framåt medan occurrencen förblir `Planned`. `OriginalScheduledDate` ligger
kvar, så uppskjutning kan inte dölja att något är försenat.

Alternativet – en `Deferred`-status – gör frågan "är detta fortfarande ogjort?" tvetydig och
kräver att varje query hanterar två utestående tillstånd.

### Beslut: `TaskAssignment` är en egen entitet — `IMPLEMENTED`

`TaskOccurrence` bär fortfarande `AssignedMemberId`, `CompletedByMemberId` och `CompletedAt`
direkt - det ändras inte. `TaskAssignment` tillkommer separat, som historik: en rad per
tillfälle någon tilldelas ett roterande arbete, oberoende av vad som sen händer med
occurrencen (omfördelning, uppskjutning). `RotationPicker` (Application) läser den senaste
raden för en `TaskDefinition` och ger turen till nästa aktiva medlem i join-ordning.

`TaskCompletion` som egen entitet är fortfarande `PROPOSED` - inget verkligt krav (delvis
slutförande, ångrad completion) har uppstått än.

### Beslut: `RecurrenceRule` — `IMPLEMENTED`

Ett självständigt value object på `TaskDefinition.Recurrence`: daily/weekly/monthly, inklusive
"var Nde vecka/månad" och "tredje tisdagen i månaden". Beräknar bara "nästa datum på eller
efter X" via stegning framåt - inget kalenderbibliotek, ingen closed-form-matematik.
`TaskDefinition.PreferredWeekday` är kvar oförändrad som ett fristående, manuellt
schemaläggningshint.

**Vem genererar `TaskOccurrence` (löser §10):** on demand, i `EnsureOccurrencesGenerated`,
anropad från `GetDailyPlan` varje gång en medlems dag hämtas. Inget schemalagt jobb, ingen
bakgrundstjänst. Generering är begränsad till det som redan är förfallet (upp till "idag"),
med ett hårt tak (366) som skydd mot en flodvåg av uteblivet arbete efter lång frånvaro - inte
avsett att någonsin nås i normal drift.

**Hur roterande ansvar räknas ut (löser §10):** `RotationPicker` väljer, för varje ny
tilldelning, den aktiva medlem som just nu ligger längst under sin rimliga andel av
hushållets roterande arbete - andel räknat mot `WeeklyTimeBudget.TotalWeeklyMinutes`, inte ett
enkelt "nästa i tur". Detta ersatte ett tidigare, enklare schema ("den som gjorde det senast →
nästa i join-ordning, annars den som gick med först") som gick sönder på två sätt en riktig
körning träffade direkt: (1) en definition utan historik föll alltid tillbaka på den som gick
med först - ett hushåll som skapar många roterande uppgifter i samma sittning (det normala
sättet att sätta upp Områden) fick dem ALLA på samma person, eftersom ingen av dem hade
historik än; (2) strikt växelvis tilldelning antar att alla kan avvara lika mycket tid, vilket
inte stämmer för en heltidsarbetande bredvid en pensionär. `EnsureOccurrencesGenerated`
uppdaterar en delad, i minnet hållen "tilldelade minuter per medlem"-tabell efter varje
tilldelning inom samma körning - annars skulle problem (1) bara flytta sig till nästa uppgift
i samma batch. Se `EnsureOccurrencesGeneratedTests` för den faktiska matematiken.
`TaskAssignment.EstimatedMinutes` är ett snapshot (samma resonemang som
`TaskOccurrence`s egna snapshots) så en senare ändring av en uppgifts tidsuppskattning aldrig
retroaktivt ändrar hur mycket en redan gjord tilldelning räknades som.

**Dagligt tak (2026-09-07), fixad efter produktionsrapport:** kvoten ovan är räknad mot ALL tid
någonsin - exakt rätt för att avgöra vems TUR det är, men ett hushåll som hade en stor obalans
innan den här kvot-modellen fanns (eller innan en medlem lades till, eller efter en lång paus)
har en medlem som ligger långt under alla andra i kvot under en lång period därefter. Utan
spärr gick VARJE tilldelning i en batch som skapar många roterande uppgifter samtidigt (återigen
det normala sättet att sätta upp Områden) till just den medlemmen tills kvoten hann jämna ut sig
- en hel dags backlog dumpad på den som har MINST utrymme att avvara just den dagen, eftersom
kvoten inte säger något om huruvida dagen faktiskt räcker till. Konkret orsak till en
produktionsrapport: en heltidsarbetande medlem (30 min/vardag) fick 18 av 19 nya roterande
uppgifter en och samma dag, eftersom en pensionärsmedlem (60 min/dag) tidigare varit kraftigt
överbelastad och därför låg långt under i kvot.

`RotationPicker.PickNext` föredrar nu, bland de som annars skulle valts på kvot, den som
fortfarande har rum kvar i sin EGEN dag (`WeeklyTimeBudget.MinutesFor` för det aktuella
datumet) för just den här uppgiften - `EnsureOccurrencesGenerated` håller en andra, i minnet
hållen tabell (`assignedMinutesByDate`, laddad en gång per datum den faktiskt når, från
`ITaskAssignmentRepository.GetAssignedMinutesByMemberOnDateAsync`) vid sidan av den all-tid-tabell
som redan fanns. Först när INGEN har utrymme kvar den dagen faller valet tillbaka på kvoten
ensam, så uppgiften ändå får en ägare - `DailyPlanner` är fortfarande det som avgör, per
medlem, vad som faktiskt får plats kontra vad som väntar till en annan dag.

### Beslut: Pausa hushåll eller enskild medlem — `IMPLEMENTED`

`Household.PausedUntil`/`HouseholdMember.PausedUntil` (båda nullable `DateOnly`, "till och med
detta datum") täcker resande: hela hushållet reser tillsammans, eller en enskild medlem gör
det medan resten är hemma. Ingen ny status, inget separat "resa"-objekt - bara ett datum att
jämföra dagens datum mot (`IsPausedOn`).

Pausning läses uteslutande av `EnsureOccurrencesGenerated`, inte av `DailyPlanner` eller några
redan skapade occurrences - en paus påverkar bara vad som *skapas* framöver, aldrig arbete som
redan låg på kalendern innan pausen sattes. Tre fall:

- **Hela hushållet pausat en given dag:** inget genereras alls den dagen, för någon uppgift.
- **En roterande uppgift, en pausad medlem:** `RotationPicker` utesluter medlemmen ur
  `eligible` för just det datumet (samma mönster som `RequiresAdult`/barn-uteslutning) -
  uppgiften går bara till någon annan; om alla är pausade blir occurrencen helt enkelt
  otilldelad, samma fallback som redan finns när ingen är vuxen.
- **En fast (icke-roterande) uppgift vars ägare är pausad:** occurrencen skapas inte alls den
  dagen - att skapa den ändå och lämna den otilldelad vore bara att skjuta upp samma problem.

**Ingen eftersläpning vid återkomst** var ett uttryckligt krav: en kalenderåterkommande
uppgift får inte hopa sig till en flodvåg av missade tillfällen dagen pausen lyfts. Lösningen
är att `GenerateOnScheduleAsync`s catch-up-loop (som redan steg fram en dag/vecka/månad i
taget upp till `MaxCatchUpPerDefinition`) fortsätter stega fram genom pausade datum precis som
vanligt, men hoppar bara över själva genereringen för de datum pausen täcker - loopens egen
räknare räknas upp även för överhoppade datum, så en ovanligt lång paus fortfarande möter
samma skyddstak som annars finns mot en flodvåg. Nästa gång hushållet öppnar appen efter
pausen har `recurrence.NextOnOrAfter` redan stegat förbi hela pausfönstret, som om
tillfällena aldrig förfallit. "Vid behov"-uppgifter (`StaleAfterDays`) behöver ingen
motsvarande stegning - de har inget kalenderdatum att tappa, en paus gör dem bara kvar "due"
tills den lyfts, utan något extra tillstånd att hantera.

`PUT .../households/{id}/pause` och `PUT .../households/{id}/members/{id}/pause` tar samma
body (`{ until: date? }`) - `null` återupptar omedelbart. `Hushall.razor` har både en
hushållsomfattande pausruta och en per-medlem-knapp i medlemslistan.

### Beslut: `TaskDefinition.StaleAfterDays` för "vid behov" — `IMPLEMENTED`

Ett fjärde schemaläggningssätt utöver `RecurrenceRule`, för uppgifter utan en naturlig
kalendercykel ("putsa fönster", "damma ytor"): i stället för nästa kalenderdatum frågar den
bara "har det gått för lång tid sen den senast blev klar?". Medvetet en egen, oberoende
egenskap på `TaskDefinition` snarare än ytterligare en `RecurrenceFrequency` - de två delar
inget beteende (`RecurrenceRule.NextOnOrAfter` stegar framåt kalendermässigt oavsett
completion; "vid behov" bryr sig bara om den senaste completion-tiden, eller skapelsetid om
uppgiften aldrig blivit klar) och att tvinga in det i samma value object hade gjort
`Advance`/`NextOnOrAfter` otydliga. En uppgift har antingen `Recurrence` eller
`StaleAfterDays`, aldrig båda samtidigt - `EnsureOccurrencesGenerated` grenar på vilken som är
satt. Till skillnad från kalenderåterkommande uppgifter (som kan hinna i kapp flera missade
tillfällen, upp till taket 366) genererar "vid behov" som mest en utestående occurrence åt
gången - `ITaskOccurrenceRepository.HasOutstandingAsync` förhindrar att en andra läggs på
innan den första är klar.

Klienten sätter för närvarande ett fast standardintervall (21 dagar,
`RoomTemplateTask.AsNeededDefaultDays`) i stället för att fråga efter ett antal dagar - se
DESIGN.md §6b för samma resonemang som bär `HouseholdRolePresets` och `RoomTemplates`.

**Frekvensen är redigerbar efter skapandet** (`UpdateTaskFrequency`, `PUT
.../tasks/{taskId}/frequency`, `RoomTasks.razor`s "Ändra frekvens") - hur ofta samma syssla ska
göras varierar mycket mellan hushåll, och tidigare gick det bara att sätta en gång, vid
skapandet; att ändra krävde att ta bort och skapa om uppgiften. Precis som vid skapande är
`Recurrence`/`StaleAfterDays` ömsesidigt uteslutande - att sätta det ena rensar alltid det andra.

**Intervallet (var N:e dag/vecka/månad) är nu ett synligt, redigerbart fält** (2026-09-07) -
tidigare hårdkodade `RoomTasks.razor`s `BuildRecurrence` alltid `Interval: 1` för Daily/Weekly/
Monthly, med ett särfall som bevarade (men aldrig visade) en malluppgifts dolda intervall (t.ex.
mallarnas "två gånger i veckan", lagrat som Daily med `Interval: 3` - se
`RoomTemplateTask.ToScheduling`). Efterfrågat konkret: hushåll vill kunna sätta "varannan
vecka" eller "var tredje månad" för rum som används mer sällan, inte bara `RecurrenceFrequency`
själv. `BuildRecurrence` tar nu intervallet som en explicit parameter i stället för att gissa det
- fältet visas (med rätt enhet: dagar/veckor/månader) närhelst en frekvens med ett intervall är
vald, både vid skapande och redigering, och förifylls med uppgiftens faktiska intervall när man
öppnar redigeringen. Det gamla särfallet för att bevara en dold mall-intervall behövs inte
längre, eftersom intervallet aldrig är dolt nu.

**Ändra frekvens för ett helt rum på en gång** (`RoomTasks.razor`s "Ändra frekvens för hela
[rum]") - ett rum som används mycket mer sällan än andra (efterfrågat konkret: sällananvända
sovrum, jämfört med en tvättstuga/hall som används varje dag) behöver ofta samma nya frekvens på
alla sina uppgifter, och att göra det uppgift för uppgift är omständligt. Ren klientsidig
loop över `UpdateTaskFrequencyAsync`, en gång per synlig uppgift i rummet - ingen ny endpoint,
eftersom mängden uppgifter per rum alltid är litet. Varje uppgifts ankardag förskjuts med sin
position i rummet (samma spridningsidé som `RoomTemplateTask.ToScheduling`), så att en hel
rumsomläggning till t.ex. "var 4:e vecka" inte klumpar ihop alla rummets uppgifter på samma dag.

**Bugg hittad i produktion (2026-09-07), fixad:** att ändra frekvens flyttar bara definitionens
framtida schema - en redan skapad, ännu ej avklarad occurrence från den GAMLA regeln blev kvar
orörd. Den hamnade aldrig i fas med den nya regeln (dess `OriginalScheduledDate` är satt en
gång för alla) och sköts bara upp dag efter dag för evigt, samtidigt som
`EnsureOccurrencesGenerated` skapade en helt ny, korrekt occurrence den dag den nya regelns
veckodag kom. Samma syssla dök upp två gånger permanent - konkret orsak till att en användare
rapporterade att "torka golv" och "töm soptunna" kändes som att de dök upp orimligt ofta (varje
rum har sin egen, medvetet spridda "Torka golvet", men dubbleringen gjorde att hälften av dem
aldrig försvann). `UpdateTaskFrequency` avfärdar nu (`TaskOccurrence.Skip`) alla utestående
occurrences för uppgiften när schemat FAKTISKT ändras (jämfört mot det gamla värdet - att spara
oförändrat val rör inte en uppgift någon redan ska göra idag). Samma klass av bugg fanns latent
i `RebalanceSchedule`, som i stället flyttar (inte avfärdar) en utestående occurrence till den
nya ankardagen via `DeferTo` - `EnsureOccurrencesGenerated` litade bara på cursorn
(`FindMostRecentOriginalDateAsync`, medvetet oförändrad av `DeferTo`) och visste därför inte att
den flyttade occurrencen redan täckte den dagen. Generatorn kollar nu explicit
(`HasOutstandingOnDateAsync`) om en utestående occurrence redan sitter exakt på datumet den är
på väg att skapa en ny för, oavsett hur den hamnade där - ett generellt skydd mot just den här
klassen av dubblettbugg, oavsett framtida orsak.

### Beslut: `Area`/`HouseholdMember`/`TaskDefinition` kan tas bort — `IMPLEMENTED`

Alla tre hade redan `Deactivate()`/`Reactivate()` i domänen (och `IsActive` i kontraktet) sen
tidigare, men ingen use case eller endpoint exponerade det. `DeactivateArea`,
`DeactivateHouseholdMember` och `DeactivateTaskDefinition` (Application) följer samma mönster
som `SetMemberWeeklyBudget`: hämta raden, mutera, `UpdateAsync`. `DeactivateArea` kaskaderar
till rummets egna aktiva uppgifter (`ITaskDefinitionRepository.ListActiveByAreaAsync` +
`UpdateAsync`) - annars skulle en borttagen station lämna kvar uppgifter som fortsätter dyka
upp varje vecka. `DeactivateHouseholdMember` kaskaderar inte: `RotationPicker` filtrerar redan
bort inaktiva medlemmar och återställer rotationen från början om den senast tilldelade inte
längre är aktiv. `DeactivateTaskDefinition` har inget att kaskadera till - en uppgift har inga
egna barn-entiteter.

Efterfrågat konkret: `Områden`-sidan absorberade hela den tidigare `Uppgifter`-sidan (nu
borttagen, se `RoomTasks.razor`/HANDOFF.md), eftersom uppgifter i praktiken alltid hör till ett rum.
Varje rumskort listar och hanterar sina egna uppgifter inline; ett `TaskDefinition`-borttag var
den saknade pusselbiten för att kunna rätta ett rum som redan skapats, inte bara filtrera bort
en mall-uppgift innan skapandet (se `RoomTemplates`-kryssrutorna, §6b i DESIGN.md).

### Beslut: `HouseholdMember.Role` som sparad egenskap, `TaskDefinition.RequiresAdult` — `IMPLEMENTED`

Rollen (`HouseholdRole`: `AdultFullTime`/`ChildOrTeen`/`Retired`) fanns tidigare bara som en
klientsidig gissning - `HouseholdRolePresets.Match` jämförde en medlems sparade veckobudget mot
de tre rollmallarnas budgetar och visade "Anpassad tid" om ingen matchade. Det räckte för att
visa rätt val i rollväljaren, men gick sönder så fort budgeten redigerades för hand efteråt, och
gav ingen sanning en backend-regel kunde luta sig mot. Rollen är nu ett riktigt, nullable fält
på `HouseholdMember` (`SetRole`), satt av samma val som redan sätter veckobudgeten (`AddMember`/
`PUT .../members/{id}/role`, båda anropen körs parallellt från klienten - se nedan).

Motivet var konkret: vissa uppgifter (fönstertvätt i sovrumsmallen) passar inte barn, oavsett
hur rolig deras vecka annars ser ut. `TaskDefinition.RequiresAdult` är en mjuk spärr -
`RotationPicker` hoppar över medlemmar vars roll är `ChildOrTeen` när den är satt, men faller
tillbaka till hela listan om det inte finns någon kvar (samma "stale rotation ska aldrig
blockera schemaläggning"-princip som redan gäller när senast tilldelade lämnat hushållet).
`Role` är `null` tills någon uttryckligen väljer en roll eller sätter tiden för hand - precis
som tidigare, bara sant lagrat i stället för återskapat via gissning.

`Hushall.razor`s rollväljare gör nu två oberoende PUT-anrop (roll, veckobudget) samtidigt med
`Task.WhenAll` i stället för i sekvens - att köra dem efter varandra fördubblade
rundresetiden till servern helt i onödan, eftersom de inte beror på varandra, och gjorde ett
redan tajmningskänsligt E2E-test (`HushallTests.Changing_a_members_role_...`) flakigare.

### Beslut: 65/35 target split mellan Pensionär och Heltidsarbetande — `IMPLEMENTED`

Den eftersträvade fördelningen av roterande hushållsarbete mellan en pensionär och en
heltidsarbetande medlem ska vara 65 %/35 %, räknat i uppskattade minuter - inte antal uppgifter.

**Var kvoten kommer ifrån.** `RotationPicker` (helt rollneutral, se `docs/ARCHITECTURE.md` §3
"Hur roterande ansvar räknas ut") väger redan varje tilldelning mot
`WeeklyTimeBudget.TotalWeeklyMinutes` - aldrig mot en roll. 65/35 uppnås därför INTE genom att
lägga in en regel som känner igen "Pensionär" eller "Heltidsarbetande" - det uppstår av sig
självt när de två rollernas `HouseholdRolePresets`-budgetar (klientsidan) står i förhållandet
7:13. `AdultFullTime` och `Retired` är nu enhetliga per dag (inte "mindre på vardagar, mer på
helgen" som tidigare - vilket dagar som faktiskt är lediga varierar med yrke, skiftarbete inom
t.ex. vård eller handel har sällan lördag/söndag ledigt): 35 min/dag (245/vecka) respektive 65
min/dag (455/vecka). 455/(245+455) = 65,0 %. `ChildOrTeen` är oförändrad - ligger utanför detta
beslut.

**Migrering av redan sparade hushåll.** `Hemordna.Application.Households.RefreshRolePresetBudgets`
uppdaterar en medlems budget till den nya preset-formeln, men ENDAST om den nuvarande budgeten
exakt matchar den GAMLA formeln för medlemmens sparade `Role` - en verkligt handredigerad budget
(eller en medlem utan roll) rörs aldrig. Formlerna (gammal och ny) är medvetet duplicerade som
literaler i den filen, eftersom `Hemordna.Client.Support.HouseholdRolePresets` är en
klient-endast typ (klienten har redan sin egen kopia av varje wire-kontrakt istället för att
referera server-assemblies, se `Hemordna.Client.Contracts.ApiContracts`s filhuvud) och
Application-lagret därför inte kan referera den direkt. Körs aldrig automatiskt - bara explicit
via `POST .../members/refresh-role-budgets`.

**Ombalansering av redan tilldelade uppgifter.**
`Hemordna.Application.Tasks.RebalanceTaskAssignments` är den nya, explicita motsvarigheten för
uppgifter som redan har en ansvarig när kapaciteterna ändras (ny roll, ny medlem, paus som tar
slut, eller just detta 65/35-beslut) - `RotationPicker` fattar bara beslut framåt, en gång per
ny occurrence, och rör aldrig ett redan fattat beslut. Flyttar bara utestående
(`TaskOccurrenceStatus.Planned`), roterande, ej arkiverade occurrences - en avklarad, överhoppad
eller arkiverad uppgift rörs aldrig, och en fast (icke-roterande) uppgift (t.ex. någons eget
sovrum) är aldrig en kandidat alls, eftersom hushållet redan uttryckligen valt en permanent
ägare. Domänen saknar helt ett "låst tilldelning"/"manuell kontra automatisk"-koncept i skrivande
stund - alla andra utestående roterande occurrences är därför flyttbara oavsett hur de en gång
tilldelades.

Algoritmen är en enda deterministisk genomgång (sorterad efter datum, definition, id) - vid varje
occurrence är kandidatpoolen exakt `RotationPicker.EligibleMembers` skuren mot
`RotationPicker.HasRoomToday` (samma regler den levande tilldelningen redan följer, så en
omflyttning aldrig kan hamna hos någon `RotationPicker` själv skulle ha avvisat). Den nuvarande
ägaren behålls om inte någon ANNAN kandidat har en strikt lägre löpande kvot (tilldelat hittills
÷ målandel) - oavgjort (inklusive "nuvarande ägare har redan lägst kvot") favoriserar att behålla,
vilket är precis det som gör hela genomgången minimal-ändring, deterministisk och idempotent (ett
nytt körning utgår från föregåendes eget resultat, där ingen kandidat längre har en strikt bättre
kvot att erbjuda - se `RebalanceTaskAssignmentsTests` för det fullständiga argumentet och flera
handverifierade exempel, bland annat ett där ojämna, odelbara uppgiftsstorlekar gör att den
nuvarande fördelningen redan är närmast möjliga och inget flyttas).

Varje ändring appliceras på redan spårade entiteter i minnet; `ITaskOccurrenceRepository.UpdateAsync`
anropas exakt en gång i slutet (dess nuvarande implementation sparar alla väntande ändringar för
hela enhetsarbetet i ett anrop) - hela satsen committas tillsammans eller inte alls.
`IHouseholdNotifier.NotifyOccurrencesChangedAsync` anropas likaså högst en gång, bara om något
faktiskt ändrades. Körs aldrig automatiskt - bara explicit via `POST .../tasks/rebalance-assignments`,
bakom samma `HouseholdAccessFilter` som `RebalanceSchedule` redan använder (ingen ny
admin-behörighetsnivå - se CLAUDE.md §12 Scope control om varför ett sådant system inte byggs
för detta).

### Beslut: `TaskWorkload` - veckoestimat viktat efter frekvens — `IMPLEMENTED`

`Omraden.razor`s ursprungliga "Totalt: X uppgifter · Y min" (`_tasks.Sum(t => t.EstimatedMinutes)`)
är en platt engångssumma - en daglig 2-minuters syssla väger lika tungt i den som en månatlig
10-minuters, trots att den dagliga i praktiken kräver ~30x så mycket tid över en månad. Ett
hushåll kan alltså inte använda den summan för att bedöma om t.ex. tolv rum är rimligt för två
personer. `Hemordna.Client.Support.TaskWorkload.WeeklyMinutes` räknar i stället varje uppgifts
förväntade andel av en genomsnittsvecka utifrån dess egen frekvens (Daily: `7/Interval`,
Weekly: `1/Interval`, Monthly: `7/(30.44*Interval)` - oavsett om den är ankrad till dag-i-månaden
eller en "n:te veckodag", båda faller ungefär en gång per `Interval` månader; "vid behov":
`7/StaleAfterDays`). En uppgift utan vare sig `Recurrence` eller `StaleAfterDays` (schemaläggs
för hand) bidrar 0 - den har ingen löpande kadens att projicera framåt.

Klientsidig, ren beräkning på redan hämtade `TaskDefinitionResponse` - ingen ny endpoint, inget
sparat. Visas bredvid den gamla totalen (inte i stället för) på Områden-sidan, tillsammans med
hushållets samlade veckokapacitet (`WeeklyTimeBudgetMinutes` summerat över alla aktiva medlemmar)
så jämförelsen blir direkt synlig. Samma undantag som den gamla totalen redan var till
DESIGN.md/PRODUCT.md §4/§8:s princip om att aldrig visa minuter i den dagliga uppgifts-UI:n -
det här är fortfarande bara ett planeringsstadie-verktyg.

### Beslut: Hushållsomfattande dagsring på "Senaste händelser" — `IMPLEMENTED`

Efterfrågat konkret: gör den platta händelseloggen roligare - ett "scorecard" föreslogs, men
avvisades direkt mot PRODUCT.md §8 ("Hemordna använder inte skuldbeläggande språk och jämför
inte hushållsmedlemmar med varandra") och CLAUDE.md §12 (gamification/streaks explicit
avplockat ur scope). Löst i stället som en delad, ICKE-jämförande känsla: `Hushall.razor`
grupperar loggen per kalenderdag ("Idag"/"Igår"/datum) och visar en liten cirkulär
progress-ring bredvid varje dag - hur stor andel av HELA HUSHÅLLETS uppgifter den dagen som
blivit klara, aldrig uppdelat per medlem.

`Hemordna.Application.Households.IHouseholdDailyActivityQuery` (ny) räknar, för varje dag i ett
fönster (`GET .../activity/daily-summary?days=`), `TaskOccurrences` grupperat på
`ScheduledDate` - totalt antal och antal med `Status = Completed`, över hela hushållet. Detta är
en ANNAN dagsindelning än den befintliga `IRecentActivityQuery` (som grupperar på `CompletedAt`
- när något faktiskt bockades av): en uppgift schemalagd på måndag men avklarad på tisdag räknas
i måndagens NÄMNARE (den hörde dit) men i tisdagens logg-post (det var då det hände). Denna
lilla avvikelse är en medveten, acceptabel förenkling - ringen är en känsla, inte en exakt
rapport.

Ringen SVG:as med en cirkel `r="15.9155"` - vald just för att dess omkrets blir exakt 100, så
`PercentComplete` (0–100) kan skrivas direkt som `stroke-dasharray` utan omräkning.

Nämnaren (`Total`) räknar INTE `TaskOccurrenceStatus.Skipped`. En överhoppad förekomst ("behövs
inte den här gången", t.ex. kvarlämnad av en frekvensändring - se `TaskFrequencyTests`) är ett
medvetet beslut att krympa dagens omfång, inte en ouppfylld post. Om den låg kvar i nämnaren
skulle ringen aldrig kunna nå 100% igen den dagen oavsett vad som faktiskt blir klart - en tyst,
permanent "ofullständig"-markering som motverkar PRODUCT.md §8:s skuldfria ton. Upptäckt av en
användare vars hushåll hade flera överhoppade förekomster kvar från tidigare frekvensändringar.

### Beslut: `MemberPreference` — `IMPLEMENTED`

Individuell presentation (`PresentationMode`: text / bild+text / stor text / en uppgift åt
gången, plus `ImageOnly`/`ReadAloud` förberedda) och motivationsnivå (`MotivationLevel`: Ingen
/ Lugn) finns som `MemberPreference`, en rad per medlem, satt via
`PUT .../members/{id}/preferences` och redigerbar i `Installningar.razor`.

`ImageAndText` läses av `MinDag.razor` (`ShowIcons`): en ikon per uppgift, gissad från
uppgiftens namn (`Hemordna.Client.Support.TaskIcons.For`) med området som reserv om inget
nyckelord i namnet träffar. Inga riktiga foton - en fri text-uppgift som "Diska köket" har
inget foto att matcha mot, så det blev självhämtade SVG-pictogram i stället (fungerar offline,
ingen bildhantering per uppgift). Bildkällan är medvetet ett riktigt AAC-pictogramsystem
(Mulberry Symbols, CC BY-SA 4.0, inget kommersiellt förbud) snarare än en generisk ikonuppsättning,
se `wwwroot/icons/tasks/NOTICE.txt` för attribution och exakt vilken symbol varje fil kommer
från. Två koncept (`air`, `shopping_cart`) saknar en bra Mulberry-motsvarighet och är kvar som
Google Material Symbols (Apache 2.0) - blandade licenser i samma mapp är avsiktligt, se
NOTICE.txt. Listan med nyckelord är medvetet liten - ny ikon krävs bara när ett vanligt
förekommande ord saknar träff, inte i förväg för varje tänkbar uppgift.

### Identifierare

`Guid` genomgående i denna fas. Genereras i domänen (`Guid.NewGuid()` i statiska factories).
Ett byte till sekventiella guider av indexskäl är en ren infrastrukturändring och behöver
inte beslutas nu.

---

## 4. Household som säkerhetsgräns — `IMPLEMENTED`

Varje entitet som kan nås av en klient bär `HouseholdId`, även när den är nåbar via en
förälder. `MemberAvailability` och `TaskOccurrence` har `HouseholdId` denormaliserat just för
att varje query ska kunna household-scope:as direkt, utan join.

### Autentisering

Egen JWT-utgivare med ASP.NET Core Identity som användarlagring. Identity sköter
lösenordshashning – Hemordna implementerar aldrig egen lösenordshantering.

- `POST /api/auth/register` och `POST /api/auth/login` returnerar en bearer-token.
- Signeringsnyckeln kommer från `Jwt__SigningKey` i miljön. Den har ingen default, och
  API:t vägrar starta om den saknas eller är kortare än 32 byte. Ingen nyckel ligger i repot.
- Login svarar likadant på okänd e-post som på fel lösenord, så endpointen inte kan användas
  för att kartlägga vilka adresser som är registrerade.

Token bär bara vem den anropande är. **Hushållstillhörighet ligger medvetet inte i en
claim** – den skulle bli gammal i samma stund medlemskapet ändras. Den slås upp per anrop.

### Lösenordsbyte och glömt lösenord

- `POST /api/auth/change-password` (kräver inloggning) verifierar nuvarande lösenord via
  Identitys `ChangePasswordAsync` innan det nya sparas.
- `POST /api/auth/forgot-password` svarar `200` oavsett om adressen finns, av samma skäl som
  login ovan. Finns kontot genereras en Identity-återställningstoken och ett mejl skickas med
  en länk till `/aterstall-losenord`.
- `POST /api/auth/reset-password` tar emot e-post + token + nytt lösenord och anropar
  Identitys `ResetPasswordAsync`.
- E-post skickas via `IEmailSender` (`Hemordna.Infrastructure.Email`). `ResendEmailSender`
  används när `Resend:ApiKey` är satt (produktion); annars loggas mejlet till `DevEmailOutbox`
  i stället för att skickas, och kan läsas tillbaka via `GET /api/auth/dev/last-email` (endast
  `Development`) – det är så lokala körningar och E2E-tester återställningsflödet utan ett
  riktigt Resend-konto.

### Passkeys (WebAuthn)

Ett alternativ till lösenord, aldrig en ersättning – registreras bara från en redan
inloggad session (Inställningar), inte vid kontoregistrering.

- `Fido2NetLib` (paket `Fido2`) sköter själva WebAuthn-ceremonin. `Fido2:ServerDomain`/
  `Fido2:Origin` (miljövariabler i produktion, se docker-compose.prod.yml) MÅSTE matcha sidans
  verkliga ursprung exakt – webbläsaren avvisar annars ceremonin. Standardvärdet i kod pekar på
  `http://localhost:5200`, samma som E2E-fixturens klient, medvetet skilt från `App:PublicUrl`.
- `PasskeyCredentials`-tabellen lagrar bara credential-id + publik nyckel + en räknare per
  registrerad enhet – den privata nyckeln lämnar aldrig personens enhet.
- Varje ceremoni är två anrop (utmaning, sedan svar); utmaningen mellanlagras i `IMemoryCache`
  några minuter (en instans räcker, se ovan) – se `PasskeyEndpoints`.
- **Inloggning är helt användarlös** – ingen e-post eller användarnamn efterfrågas.
  Registrering kräver `ResidentKeyRequirement.Required` (en "discoverable" nyckel), så
  `login/options` kan skicka en TOM `allowCredentials`-lista och låta webbläsaren själv visa
  vilken passkey den har för sidan. Vem det är avgörs först i `login/verify`, via
  `credential.RawId → PasskeyCredentials.UserId` – ett `flowId` (inte e-post, inte
  användar-id) knyter ihop de två anropen, eftersom identiteten inte är känd förrän efteråt.
  E-post krävs fortfarande för att registrera en ny passkey (Inställningar, redan inloggad) och
  finns kvar som fallback-inloggning via lösenord om enheten saknar en passkey.
- **Viktig fälla**: `Results.Ok(options)` fungerar INTE för Fido2NetLibs egna typer – appens
  globalt registrerade `JsonStringEnumConverter` (ConfigureHttpJsonOptions i Program.cs)
  krockar med Fido2NetLibs egna per-property `[JsonConverter]`-attribut och producerar fel
  wire-värden (`"None"` i stället för `"none"`, `"PublicKey"` i stället för `"public-key"`).
  Utgående svar använder därför Fido2NetLibs egen `.ToJson()`; inkommande attestation/assertion
  läses som rå request body och deserialiseras med en egen, omodifierad `JsonSerializerOptions`
  – aldrig som en bunden minimal-API-parameter av typen ovanpå appens globala JSON-inställningar.
- `wwwroot/js/webauthn.js` + `Hemordna.Client.Services.WebAuthnClient` konverterar mellan
  webbläsarens `ArrayBuffer`-baserade WebAuthn-API och base64url-strängarna Fido2NetLib
  förväntar sig.
- E2E-testerna (`PasskeyTests.cs`) driver en riktig ceremoni via Chromiums virtuella
  autentiserare (CDP:s `WebAuthn`-domän) – ingen riktig Face ID/Touch ID-hårdvara behövs.

### Håndhävande av scoping

`HouseholdAccessFilter` är ett endpoint-filter på routegruppen
`/api/households/{householdId}`. Det körs före varje handler, så ingen handler kan glömma
kontrollen. Valet föll på ett filter framför ett globalt query-filter i `DbContext` eftersom
gränsen då syns i routingen i stället för att vara osynlig magi i persistence-lagret.

En anropare som frågar efter ett hushåll hen inte tillhör får **404, inte 403**: ett 403
skulle bekräfta att hushållet existerar för någon som inte har rätt att veta det.

### Medlemskapsmodell

En användare är för närvarande medlem i **ett** hushåll. `HouseholdMember.UserId` är nullable
– medlemmar som lagts till av någon annan, ett barn eller en partner som ännu inte
registrerat sig, har ingen användare förrän de skaffar en. Ett unikt filtrerat index på
`UserId` hindrar ett andra medlemskap från att skapas bakom applikationens rygg, och
`LinkToUser` vägrar peka om en medlem till en annan användare eftersom det tyst skulle
flytta hens historik.

**Beslut: en användare tillhör exakt ett hushåll, avsiktligt.** Detta är ingen tillfällig
begränsning som väntar på en medlemskapstabell - flera hushåll per användare ska inte byggas.
Det unika filtrerade indexet på `UserId` är produktgränsen, inte bara en fas-1-genväg.

### Beslut: `Household.InviteCode` för att gå med i ett hushåll — `IMPLEMENTED`

Fram tills nu fanns ingen väg in i ett hushåll förutom att skapa ett nytt - "Lägg till
medlem" på Hushållsöversikten skapar bara en namngiven plats utan eget konto. `JoinHousehold`
(mirrorar `CreateHousehold`) är den andra halvan: `POST /api/households/join` slår upp
hushållet på dess `InviteCode` i stället för att skapa ett nytt, kopplar den anropande
användaren som ny medlem och kräver samma "en användare, ett hushåll"-koll som `CreateAsync`.

Koden är åtta tecken ur ett alfabet utan förväxlingsbara tecken (`0/O`, `1/I/L`) - menad att
läsas högt eller skrivas för hand, inte en hemlighet i säkerhetsbemärkelse (den ger bara
medlemskap, ingenting mer). Genereras i `Household.Create` (samma mönster som `Guid.NewGuid()`
redan används på andra håll i domänen) och kan bytas ut med `RegenerateInviteCode` om en kod
läckt - påverkar aldrig redan tillagda medlemmar. Ett unikt index i databasen gör kollision
till en garanti, inte bara en fråga om entropi.

---

## 5. Source of truth och synkstrategi — `IMPLEMENTED` (1–2) / `PROPOSED` (3)

Servern är source of truth. Klienten är aldrig auktoritativ.

1. HTTP mot Api för alla skrivningar. `IMPLEMENTED`.
2. `HouseholdHub` (Api, SignalR): en grupp per hushåll, `household:{id}`. Klienter joinar
   efter inloggning och får bara det egna hushållets händelser - samma gräns som i
   datamodellen, kontrollerad på samma sätt som `HouseholdAccessFilter` gör för REST.
   Application känner inte till SignalR: `IHouseholdNotifier` är gränssnittet,
   `SignalRHouseholdNotifier` (Api) implementerar det. Ett enda grovkornigt meddelande
   (`OccurrencesChanged`) snarare än ett per händelsetyp - varje klient läser om sin egen dag i
   stället för att servern behöver designa en payload per händelse innan det finns en andra
   konsument som behöver en. JWT-token skickas som query-parameter bara på hub-vägen, eftersom
   en webbläsare inte kan sätta en Authorization-header på WebSocket-handskakningen.
   `IMPLEMENTED`.
3. Offline byggs stegvis. Full offline conflict resolution är uttryckligen utanför MVP.
   `PROPOSED`.

### Concurrency — `IMPLEMENTED` (dubbel completion) / `PROPOSED` (resten)

Flera klienter kan uppdatera samma hushåll samtidigt. Hanterat i dag:

- **Dubbel completion.** `TaskOccurrence.Complete` är idempotent: att slutföra något redan
  slutfört är en no-op och skriver inte om vem eller när. En andra klient med gammal state
  kan alltså inte skriva över den första personens completion.
- **Ogiltiga övergångar.** Slutfört eller överhoppat arbete kan inte omfördelas, skjutas upp
  eller slutföras igen – det kastar `DomainException` i stället för att tyst lyckas.

`PROPOSED`: optimistic concurrency (xmin som concurrency token i Npgsql) på occurrences när
persistence byggs. Ingen ytterligare concurrency-infrastruktur införs innan det finns ett
observerat problem.

---

## 6. DailyPlanner — `IMPLEMENTED`

`Hemordna.Application.Planning.DailyPlanner` är applikationens första kärntjänst.

```text
DailyPlanRequest(MemberId, Date, AvailableMinutes, Candidates)
        │
        ▼
   DailyPlanner.Plan
        │
        ▼
DailyPlan(Items, Unplanned, PlannedMinutes, RemainingMinutes)
```

Egenskaper, samtliga avsiktliga:

- **Ren funktion.** Inga dependencies, ingen state, ingen storage.
- **Ingen klocka.** `Date` och `AvailableMinutes` skickas in. Planeraren rör aldrig
  `DateTime.Now`. Det är förutsättningen för att kunna testa med fasta datum.
- **Deterministisk.** Sorteringen är total, så samma request ger alltid samma plan oavsett
  i vilken ordning kandidaterna kommer in. Verifierat med roterad input.
- **Ingen optimering.** Sortera, sedan greedy first-fit. Ingen packningsalgoritm.

### Sorteringsregler och tie-breakers

Tillämpas i denna ordning:

| # | Regel | Varför |
|---|---|---|
| 1 | Icke uppskjutbara först | De kan inte flyttas till en annan dag alls – förlorar de budgeten är de förlorade |
| 2 | Förfallna före det som förfaller idag | Något som redan är sent ska inte fortsätta halka |
| 3 | Högre prioritet före lägre | Hushållets uttalade viktning |
| 4 | Tidigast ursprungligt förfallodatum först | Äldst arbete leder |
| 5 | Delar rum/våning med något redan valt idag (2026-09-08) | Se "Beslut: rums-/våningsklustring" nedan - bara en mjuk preferens bland redan likvärdiga kandidater |
| 6 | Kortare uppgift först | Vid lika ställning: att bli klar slår att påbörja, och mer ryms i budgeten |
| 7 | `ChoreSequenceHint.RankFor` (2026-09-07) | Ett fåtal kända "gör X före Y"-par, se nedan |
| 8 | Occurrence-id stigande | Stabil slutlig tie-break som gör ordningen total |

Regel 1 före regel 2 och 3 är ett medvetet val: en förfallen uppgift kan fortfarande flyttas,
en icke uppskjutbar kan inte det.

**`ChoreSequenceHint`** (regel 7) är en medvetet SMAL nudge, inte ett generellt
städordnings-system - efterfrågat konkret: dammsug (eller sopa) golvet innan man torkar det,
eftersom smuts annars bara flyttas runt. Ren nyckelordsmatchning på uppgiftsnamnet
(`"torka golvet"`/`"moppa"` rankas efter `"dammsug"`/`"sopa golvet"`), tillämpad EFTER allt
planeringsrelevant (uppskjutbarhet, förfallenhet, prioritet, datum, minuter, rums-/vånings-
klustring) - kan alltså aldrig ändra VAD som planeras eller skjuts upp, bara i vilken ordning
två annars helt likvärdiga uppgifter visas när de råkar hamna samma dag. Fler par kan läggas
till samma väg om ett liknande konkret behov dyker upp - ingen anledning att gissa fram en
bredare "damma före dammsug före torka"-ontologi som ingen efterfrågat.

### Urval

Greedy first-fit: gå igenom den sorterade listan och ta med allt som får plats i återstående
tid. En lång uppgift som inte får plats blockerar inte de kortare bakom sig.

Kandidater som inte längre är utestående, eller som är schemalagda till ett senare datum,
ingår varken i `Items` eller i `Unplanned` – de tillhör helt enkelt inte dagen.

Utfallet `Unplanned` heter medvetet inte `Deferred`: även en icke uppskjutbar uppgift kan
hamna där, eftersom planeraren inte kan skapa tid. En anropare som vill lyfta fram sådant
filtrerar på `Candidate.CanBeDeferred`.

**"Lägg till en extra uppgift" (Min dag) respekterar samma regel i stället för att kringgå
den.** En uppgift som läggs till för att göras i dag skapas utan upprepning (samma form som
`RoomTasks.razor`s "Ingen - schemaläggs för hand") och schemaläggs direkt för dagens datum -
men för att den garanterat ska hamna i `Items` i stället för att tyst glida till "till en annan
dag" (en nyskapad medlem börjar på noll minuter/dag) höjs dagens tillgängliga tid med exakt
uppgiftens uppskattade tid (`SetMemberAvailability`, samma mekanism som "mindre tid i dag"
används åt andra hållet). Planeraren ljuger därmed aldrig om hur mycket tid som faktiskt finns,
se `MinDag.razor.AddExtraTaskAsync`.

### Beslut: Rumsgruppering på Min dag — `IMPLEMENTED`

Vid det här stegets implementation hade `DailyPlanner` inget begrepp för "rum" i sin egen
sortering (den brydde sig om uppskjutbarhet/förfallenhet/prioritet/datum/minuter) - en dags
uppgifter från olika rum interfolierades därför fritt, vilket i praktiken kändes slumpmässigt
(efterfrågat konkret: gör klart köket innan du går till badrummet, inte köks-uppgift/badrums-
uppgift/köks-uppgift om vartannat). Löst helt klientsidigt i `MinDag.razor` här, utan att röra
`DailyPlanner`s då redan hårt testade urval/prioritering - se dock "Beslut: rums-/
våningsklustring i urvalet" längre ner, som senare (2026-09-08) faktiskt lade till en
rums-/våningsmedveten regel i själva urvalet, av samma produktskäl:

- Förfallna uppgifter (`IsOverdue`) lyfts ut i en egen ledande grupp ("Sedan tidigare"),
  oavsett rum - en redan sen uppgift ska aldrig kunna gömmas längre ner i ett rums lista.
- Resten grupperas efter `AreaName` via `IEnumerable.GroupBy`, som bevarar varje nyckels
  FÖRSTA-förekomst-ordning - rummens inbördes ordning speglar därför fortfarande vilket rums
  uppgifter `DailyPlanner` själv rankade tidigast, snarare än en godtycklig alfabetisk lista.
  En uppgift utan rum hamnar i en egen "Övrigt"-grupp.
- Ren omorganisering av en REDAN BESLUTAD lista för visning - ändrar aldrig vad som planeras
  eller skjuts upp. `Hemordna.Client.Components.TaskListItem.razor` (ny) bär den delade
  `<li>`-markeringen (bock, ikon, chip, expandera, skjut upp) så den inte behöver dupliceras
  per grupp.

**Bugg hittad i produktion (2026-09-07), fixad:** "Tjuvkika på ett schema"s peek-vy visade
`PlannedTaskResponse.IsOverdue` ingenstans, till skillnad från huvudvyn (som redan taggar en
sådan rad "sedan tidigare"). En daglig uppgift som inte avklarats idag är fortfarande
utestående i morgon - `DailyPlanner` viker (avsiktligt) in den i morgondagens `Items` som
övertidig, TILLSAMMANS med morgondagens egen, färska instans - vilket i peek-vyn visade samma
uppgiftsnamn två gånger utan förklaring och lästes som en äkta dubblett. Ingen dubblett i
databasen: två skilda occurrences (dagens obehandlade, morgondagens nya), bara samma etikett
som huvudvyn redan hade som saknades i peek. `MinDag.razor`s peek-rendering visar nu samma
"sedan tidigare"-chip för `item.IsOverdue` som huvudvyn.

### Beslut: rums-/våningsklustring i urvalet — `IMPLEMENTED` (2026-09-08)

Produktfeedback ett steg längre än ren visning: människor städar naturligt ett rum, eller en
våning, färdigt i taget - köket + det lilla wc:et, sedan nästa dag ett sovrum på övre plan +
hallen där - snarare än att hoppa mellan rum. Ren VISNINGS-omgruppering (ovan) räcker inte för
det: vilka uppgifter som över huvud taget hamnar SAMMA DAG avgörs av `DailyPlanner`s eget
urval, som fram tills nu var helt rumsblint - detta är alltså den första ändringen som rör
`DailyPlanner`s urval/prioritering sedan den beskrevs som "redan hårt testad" och medvetet
orörd ovan. Explicit avstämt med användaren innan implementation, inklusive hur strikt
(mjuk preferens, aldrig starkare) och om "rum" skulle tolkas smalt (bara exakt samma rum) eller
brett (rum ELLER våning, som i det egna exemplet) - svaret blev mjuk preferens, rum ELLER
våning.

- **Regel 5** (ny, se tabellen ovan): bland kandidater redan lika på uppskjutbarhet/
  förfallenhet/prioritet/förfallodatum, föredras en som delar kluster med något REDAN VALT för
  dagen. Ren mjuk preferens - kan aldrig lyfta en kandidat förbi något mer förfallet eller
  högre prioriterat, och den allra första uppgiften för dagen påverkas aldrig (inget är valt än
  att dela kluster med).
- **`Planning.TaskCluster.KeyFor(areaName)`** (ny, `internal`): samma rum om `areaName` saknar
  "Våning – "-prefix, annars våningen. Medvetet duplicerar samma tolkning av
  "Våning – "-namnkonventionen som klientens `Support.RoomFloors.FloorOf` redan gör - server
  och klient är separata projekt (Application refererar aldrig Client), så samma lilla, sköra
  namnkonvention-parsning finns nu på båda ställena. `DailyPlanner` själv förblir formellt
  "rumsblint" i sin egen kod (den känner bara `TaskCluster`s nyckel, aldrig "Våning – "-strängen
  själv) - dokumentationsmässigt en nyansering, inte en motsägelse: den ordnar fortfarande inga
  regler efter ett rums NAMN, bara efter om två kandidaters nycklar råkar vara lika.
  En kandidat utan rum alls har ingen klusternyckel och matchar aldrig något - två "Övrigt"-
  uppgifter klustras inte bara för att båda saknar rum.
- **Algoritmen ändrades från engångssortering till iterativt urval**, eftersom regel 5 är den
  enda som beror på VAD som redan valts för dagen så här långt - till skillnad från alla andra
  regler kan den inte uttryckas som en enda statisk sortering. Varje varv väljer den bästa
  återstående kandidaten (samma regelkedja, bara med regel 5 omvärderad mot vad som redan
  valts), tar bort den ur den återstående poolen, och upprepar - O(n²) i värsta fall, helt
  oproblematiskt för en dags realistiska kandidatantal. Samma egenskaper som förut bevarade:
  fortfarande en ren funktion (inga dependencies, ingen klocka), fortfarande deterministisk
  (verifierat med roterad input i `DailyPlannerTests`), och en lång uppgift som inte får plats
  blockerar fortfarande inte kortare uppgifter bakom sig i poolen.
- **Noll regression i den befintliga testsviten, verifierat innan någon ny test skrevs**: ingen
  av de 23 befintliga `DailyPlannerTests` sätter `areaName` alls, så `TaskCluster.KeyFor(null)`
  är `null` för varje kandidat i hela den befintliga sviten - regel 5 är därmed ett garanterat
  no-op mot allt tidigare testat beteende. Fyra nya tester lades till: klustring vinner över
  "kortast först", två olika rum på samma våning klustrar ihop, två rumslösa uppgifter klustrar
  INTE ihop med varandra, och förfallenhet vinner alltid över att fortsätta ett redan öppnat
  kluster.
- **`PlanCandidate.AreaName`** var tidigare dokumenterat "display only" - kommentaren
  uppdaterad, den driver nu även klustringen.

**Bugg hittad direkt efter driftsättning, fixad samma dag:** när "Beslut: Rumsgruppering på
Min dag" (ovan) fick sin egen uppföljning att INTE upprepa rummets namn som en chip på raden
(rumsrubriken räcker) togs våningsprefixet bort från chippen rakt av
(`Support.RoomFloors.RoomNameOf`) - men chippen visas numera BARA i "Sedan tidigare", den enda
platsen en rad saknar både rums- OCH våningsrubrik. Två olika rum med samma namn på olika
våningar (två "Hall") blev då omöjliga att skilja åt där. `TaskListItem`s chip visar nu hela
`AreaName` (våningsprefixet inkluderat) igen - eftersom chippen bara någonsin renderas i just
det kontext som saknar all annan disambiguering, är den fulla strängen alltid rätt val där.
Ny regressionstest, `MinDagDetailTests
.An_overdue_rooms_chip_keeps_its_floor_prefix_to_tell_two_same_named_rooms_apart`.

---

## 7. Persistence — `IMPLEMENTED`

- EF Core 10 med Npgsql-providern 10.0.3 mot PostgreSQL. Dev-databas i
  `.devcontainer/docker-compose.yml` (postgres:17-alpine), connection string via
  `ConnectionStrings__Hemordna`.
- `HemordnaDbContext` i Infrastructure, mappning via `IEntityTypeConfiguration<T>` och Fluent
  API i `Persistence/Configurations/`. Inga EF-attribut i Domain.
- `services.AddInfrastructure(configuration)` som DI-extension, så att Api inte känner till
  implementationsdetaljer.
- Inga generiska repositories och ingen Unit of Work-wrapper ovanpå EF Core.
  `IHouseholdRepository` är namngivet efter de use cases det tjänar, och `AddAsync`
  persisterar direkt. Ingen use case spänner ännu över mer än ett aggregat, så det finns
  inget att commita separat. Det omprövas när en use case gör det.

Domänmodellen mappades utan en enda ändring: privata setters, privata konstruktorer och
backing-fält för samlingar hanterar EF Core som de är.

Migrationen skapas med:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Hemordna.Infrastructure \
  --startup-project src/Hemordna.Api
```

Migrationen ska läsas innan den appliceras, och verifieras mot dev-databasen.
`InitialCreate` är skapad och applicerad.

### Beslut: `WeeklyTimeBudget` mappas som `integer[]`

Ursprunglig inriktning var sju kolumner. Den föll på att value objectet lagrar minuterna i
en privat array och medvetet inte exponerar någon property per veckodag – sju kolumner hade
krävt sju publika properties som bara finns för ORM:ens skull, alltså att persistence
dikterar domänen.

I stället mappas det till en native PostgreSQL `integer[]`, ordnad söndag–lördag enligt
`DayOfWeek`-värdena, via en `ValueConverter` som bara använder value objectets befintliga
publika API. Kolumnen är fortfarande queryable via array-indexering, och domänmodellen
behövde inte röras. Reversibelt via migration om verkliga queries visar att sju kolumner
behövs.

---

## 8. Första API-kontrakt och use cases — `IMPLEMENTED`

### Application

| Use case | Ansvar |
|---|---|
| `CreateHousehold` | Skapar ett hushåll med den inloggade användaren som första medlem |
| `GetHousehold` | Hämtar ett hushåll, eller inget om det saknas |
| `AddHouseholdMember` | Lägger till en person med veckobudget |
| `AddArea` | Lägger till ett område |
| `CreateTaskDefinition` | Beskriver ett nytt arbete |
| `ScheduleTaskOccurrence` | Lägger en uppgift på ett datum |
| `SetMemberAvailability` | "Mindre tid idag" utan att veckan ändras |
| `GetDailyPlan` | Löser tillgänglig tid, hämtar kandidater, kör `DailyPlanner` |

Enkla use case-klasser, inte ett generiskt repository-system. Interfaces namnges efter vad
applikationen faktiskt behöver och införs bara där Application har en verklig boundary mot
Infrastructure. `IPlanCandidateQuery` heter query, inte repository, eftersom den bara läser
och returnerar planeringsmodeller i stället för aggregat.

### Endpoints

Allt under `/api/households/{householdId}` kräver token och körs bakom
`HouseholdAccessFilter`.

| Metod | Väg | Svar |
|---|---|---|
| `GET` | `/health` | Processen och databasanslutningen. Anonym |
| `POST` | `/api/auth/register` | `201` med bearer-token. Anonym |
| `POST` | `/api/auth/login` | `200` med bearer-token, annars `401`. Anonym |
| `POST` | `/api/auth/forgot-password` | `200` alltid (se ovan). Anonym |
| `POST` | `/api/auth/reset-password` | `200`, annars `400` med felmeddelanden. Anonym |
| `POST` | `/api/auth/change-password` | `200`, annars `400`/`401`. Kräver token |
| `GET` | `/api/auth/passkeys` | Lista registrerade passkeys. Kräver token |
| `POST` | `/api/auth/passkeys/register/options` | Utmaning för att registrera en passkey. Kräver token |
| `POST` | `/api/auth/passkeys/register/verify` | `200`, annars `400`. Kräver token |
| `DELETE` | `/api/auth/passkeys/{credentialId}` | `200`, annars `404`. Kräver token |
| `POST` | `/api/auth/passkeys/login/options` | Utmaning + `flowId`, ingen e-post krävs. Anonym |
| `POST` | `/api/auth/passkeys/login/verify?flowId=` | `200` med bearer-token, annars `401`. Anonym |
| `GET` | `/api/me` | Den inloggades identitet och hushållstillhörighet |
| `POST` | `/api/households` | `201` med den skapade resursen, `409` om användaren redan har ett hushåll |
| `GET` | `/api/households/{householdId}` | `200`, annars `404` |
| `POST` | `/api/households/{householdId}/members` | `201` med medlemmen |
| `POST` | `/api/households/{householdId}/areas` | `201` med området |
| `GET` | `/api/households/{householdId}/tasks` | `200` med hushållets uppgifter |
| `POST` | `/api/households/{householdId}/tasks` | `201` med uppgiften |
| `POST` | `/api/households/{householdId}/tasks/{taskId}/occurrences` | `201` med den schemalagda instansen |
| `PUT` | `/api/households/{householdId}/tasks/{taskId}/frequency` | `200` med uppgiften, annars `404` |
| `PUT` | `/api/households/{householdId}/tasks/{taskId}/assignment` | `200` med uppgiften, annars `404` |
| `PUT` | `/api/households/{householdId}/tasks/{taskId}/area` | `200` med uppgiften, annars `404` |
| `PUT` | `/api/households/{householdId}/tasks/{taskId}/requires-adult` | `200` med uppgiften, annars `404` |
| `PUT` | `/api/households/{householdId}/areas/{areaId}/name` | `200` med området, annars `404` |
| `PUT` | `/api/households/{householdId}/members/{memberId}/availability` | `200` med dagens tidsbudget |
| `GET` | `/api/households/{householdId}/members/{memberId}/plan?date=` | `200` med Min dag |

`memberId` i vägen ovan var redan fritt valbart bland hushållets egna medlemmar - endpointen
kräver bara att anroparen tillhör hushållet, inte att `memberId` är anroparens eget. "Tjuvkika
på ett schema" (Min dag) är därför ett rent klient-tillägg: en medlem- och dagväljare
(`?date=` accepterar redan valfritt datum) som återanvänder samma anrop skrivskyddat - inga
bock- eller uppskjut-knappar, se `MinDag.razor`s `_peekDay`. Ett datum i framtiden (t.ex.
imorgon) genererar också dagens utestående förfallna uppgifter i samma veva, eftersom
`GetDailyPlan` skickar det efterfrågade datumet rakt in i `EnsureOccurrencesGenerated` som
"idag" - se dess egen kommentar om varför det är avsiktligt begränsat till ett håll (upp till
angivet datum), inte en bakgrundsprocess som springer långt före verkligheten.

Enum-värden serialiseras som namn, inte siffror: en klient som läser
`"ExceedsRemainingTime"` behöver ingen uppslagstabell, och en ny enum-medlem kan inte tyst
ändra vad ett värde betyder.

Schemaläggning av occurrences är **explicit** tills vidare. Att generera dem ur en
recurrence-regel, och om det sker on demand eller i ett schemalagt jobb, är fortfarande ett
öppet beslut – en explicit endpoint håller det öppet i stället för att avgöra det av misstag.

```json
POST /api/households
{ "name": "Familjen" }
```

API:t exponerar enkla request/response-DTO:er – aldrig domän- eller EF-entiteter direkt.
Ingen affärslogik i endpointen; den mappar och delegerar.

### Tester

`Hemordna.Application.Tests` täcker `CreateHousehold` och `GetHousehold` med fakes. De ska
inte kräva en verklig PostgreSQL-instans.

---

## 9. Teststrategi — `IMPLEMENTED`

| Projekt | Ansvar |
|---|---|
| `Hemordna.Domain.Tests` | Invarianter, ogiltig input, statusövergångar |
| `Hemordna.Application.Tests` | `DailyPlanner` – ordning, budget, determinism |

Regler:

- Tester verifierar beteende, inte implementationdetaljer. Rena getters testas inte.
- Alla datum är fasta konstanter. Inget test läser dagens datum.
- Application-tester kräver ingen PostgreSQL-instans, och ska inte göra det.
- Ingen hard-codad genväg i produktionskoden får finnas för att göra ett test grönt.

Integrationstester mot en verklig PostgreSQL införs när persistence byggs – `PROPOSED`.

---

## 10. Beslut: Ny form — `IMPLEMENTED` (steg 1–5, alla)

Klienten byggs om skärm för skärm till ett nytt visuellt uttryck och en enklare navigation -
enbart `Hemordna.Client` och dokumentation, ingen ändring i Domain/Application/Infrastructure/
Api eller i `DailyPlanner`s urval, ordning eller tidsbudget.

**Varför.** Konkreta problem med det tidigare gränssnittet, upptäckta i användning: Områden
hade två knappar per uppgift och kunde bli över 5000px hög på mobil redan med tre rum (~22
uppgifter gav 44 knappar på en sida); navigationen använde Unicode-tecken (`☀ ▤ ▦ ⌂ ⚙`) i
stället för ritade ikoner, vilket varken skalar konsekvent mellan plattformar eller bär mening
utan text bredvid; en tom Min dag visade en blå informationsruta i stället för ett lugnt,
avsiktligt tomt-läge; den gröna identiteten (`#4E9D74`) var en platshållarfärg utan förankring
i produktens ton ("ett enklare hem, en lugnare vardag").

**Vad som medvetet INTE ändrats:** `DailyPlanner`s sortering/urval/tidsbudget, produktreglerna
i PRODUCT.md (Min dag som startsida, ingen hushållsbacklogg som förstaskärm, individuell
presentation, ingen skuldbeläggning/gamification), och sidornas faktiska innehåll och funktion
i de steg som ännu inte genomförts - se arbetsordningen nedan.

### Steg 1 (`feat/ny-form-grund`) — `IMPLEMENTED`

- **Designtokens** i `wwwroot/css/app.css`: gustaviansk blå (`--gustav`) ersätter grönt helt,
  saffran (`--saffran`) reserverad för "idag"-markeringar, nya radienamn (`--radius`,
  `--radius-sm`, `--pill`). Alla befintliga komponentklasser (`.btn`, `.card`, `.chip`,
  `.list-item` m.fl.) mappar om till de nya tokennamnen. Mörkt läge är **inte** del av detta
  steg - `PROPOSED` till steg 5.
  - `--ronn` (`#C25A4A`, destruktiv/varning) klarar inte WCAG AA som text mot `--kalk` (3.90:1)
    eller `--ronn-soft` (3.45:1) - en ny token, `--ronn-ink` (`#A04128`), används i stället för
    all destruktiv knapp-/länktext (5.10–5.77:1). En medveten, dokumenterad avvikelse från
    konceptartefaktens exakta färgpar, eftersom DESIGN.md §10:s kontrastkrav är hårt.
- **Typsnitt**: Familjen Grotesk och Atkinson Hyperlegible, båda SIL OFL, självhostade som
  woff2 i `wwwroot/fonts/` (licensfiler och NOTICE.txt bredvid, samma mönster som
  `wwwroot/icons/tasks/`). Ingen Google Fonts-CDN i appen - kravet är att den ska starta
  offline. Familjen Grotesk hämtades som en enda variabel woff2-fil (weight-axel 400–700);
  fyra `@font-face`-regler (400/500/600/700) pekar på samma fil och webbläsaren renderar rätt
  vikt via filens egen axel.
- **`Icon.razor`**: inline-SVG, inga Unicode-ikoner längre. **`BottomSheet.razor`**: ark från
  botten (mobil) / centrerad dialog (≥ 640px), byggd men oanvänd till steg 3–4 tar den i bruk.
- **Navigation**: fyra flikar (Idag/Rum/Vecka/Hushåll), identisk ordning på mobil och dator -
  se DESIGN.md §8. `/omraden`, `/planering`, `/mer` lever kvar som omdirigerande sidor
  (`OmradenRedirect.razor` m.fl.) så inga befintliga länkar bryts. Inställningar och Logga ut
  har inte längre egna flikar - tills vidare två listrader längst ned på `Hushall.razor`
  (samma mönster `TaskOptionsSheet`/`MemberSheet` ersätter i steg 3–4), eftersom `/mer` annars
  hade blivit en återvändsgränd.
- **Desktop-rail**: `MainLayout`/`NavMenu` byter den gamla 260px-sidopanelen mot en ~72px rail
  och centrerar sidinnehållet på max 640px.

**Bugg hittad under obligatorisk skärmbildsgranskning, fixad (utanför den ursprungliga
steg-1-listan, men ett rent CSS-fel som gjorde varje ocheckad uppgift på Min dag oanvändbar):**
`Components/TaskListItem.razor` saknade en egen `.razor.css`. Blazors CSS-isolering ger bara en
underkomponents **rotelement** den anropande sidans scope-attribut - element som är nästlade
djupare inuti underkomponentens egen markup (bocken, ikonen, namnet, pil-knappen) fick inget
scope alls och renderades helt ostylade (en hopklämd standardknapp i stället för en 24×24px
cirkel). Introducerades när `TaskListItem.razor` bröts ut ur `MinDag.razor` (se §6, "Beslut:
Rumsgruppering på Min dag", 2026-09-07) utan att en motsvarande `.css`-fil skapades - synligt
först nu eftersom det inte fanns någon skärmbild av en Min dag med riktiga, ocheckade uppgifter
förrän detta stegs verifiering krävde en. `Components/TaskListItem.razor.css` är nu
självförsörjande: den upprepar `.task`/`.task-details` (som redan fungerade via
scope-propagering) tillsammans med de tidigare ostylade nästlade reglerna, snarare än att lita
på att `Pages/MinDag.razor.css` täcker dem.

**Upptäckt i steg 1, löst i steg 2:** "Stor text" och "En uppgift åt gången" sparades korrekt
som `MemberPreference.Presentation` men lästes aldrig av `MinDag.razor` - se steg 2 nedan.

### Steg 2 (`feat/ny-form-idag`) — `IMPLEMENTED`

Min dag byggdes om till Idag enligt DESIGN.md §6 - grupperad lista, chips i stället för
utfällningar, bock- och svep-interaktion, samt de två presentationslägena steg 1 hittade som
sparade men overksamma.

**Beslut, ingen fråga innan implementation:**
- **Tid förblir dold, tvärtemot konceptets egen skiss.** Konceptartefaktens `TaskListItem`-
  beskrivning ("minuter till höger med tabular-nums") och summeringsraden ("42 min kvar")
  motsäger DESIGN.md §6a rakt av - ett medvetet, dokumenterat beslut från tidigare
  produktfeedback ("alltför mycket tidsvisning... skapar stress"). Frågan ställdes uttryckligen
  innan kod skrevs (se konversationen); svaret var att behålla §6a orört. "Idag" visar därför
  bara "N av M klara", ingen minutsiffra någonstans, varken per uppgift eller summerat.
- **Områdeschippen per uppgift behölls**, trots att konceptets egen mockup inte visar den (bara
  ett rumsrubrik-baserat sammanhang) - `MinDagDetailTests.Shows_the_area_as_a_chip_...` skyddar
  redan detta som ett namngivet, avsiktligt beteende (DESIGN.md §6, gamla texten "kryssruta,
  namn, områdeschip, expandering"), och inget i uppdraget bad om att ta bort det.
- **Ingen ny knapp för "flytta"** - `TaskListItem`s befintliga expandera-knapp följt av "Skjut
  upp till imorgon" var redan den permanenta, tangentbords-/skärmläsarvänliga vägen att skjuta
  upp en uppgift (uppfyller redan kravet att bock- och flytta-handlingar alltid finns som
  vanliga knappar); svepet är ett rent tillägg ovanpå den, inte en ersättning.
- **`.chip-action`** (ny, global, i `app.css`): konceptets egna chips är ritade betydligt under
  44px - DESIGN.md §10 är uttryckligt ovillkorligt ("gäller oförändrat"), så "Flytta till en
  annan dag"/"Extra uppgift" är riktiga knappar med `min-height: 44px`, bara chip-formade.
- **"Nästa: veckodag, N uppgifter" i tomt-läget** har ingen egen endpoint att fråga - löst
  genom att stega framåt dag för dag (samma `GetDailyPlanAsync`-anrop "Idag" redan gör, vilket
  redan genererar den dagens förekomster som en sidoeffekt, precis som gamla "Tjuvkika" på
  imorgon alltid gjort) och sluta vid 7 dagar. En vecka är gränsen för vad "Nästa: ..." rimligen
  ska antyda - längre bort står "Ledigt idag" bättre för sig själv.
- **"En uppgift åt gången"** visar bara den första uppgiften i samma ordning listan redan har
  (`OverdueItems` följt av `RoomGroups`, plattat till en enda kö) - inget separat index: att
  bocka av eller skjuta upp laddar om dagen, och nästa uppgift blir automatiskt densamma kön.

**Presentationslägena kopplades in:**
- **"Stor text"**: `MainLayout.razor` läser nu `MemberPreference.Presentation` en gång per
  inloggad medlem (inte per sidnavigering - se `_textSizeAppliedForMember`) och sätter
  `data-text-size="large"` på `<html>` via `wwwroot/js/text-size.js`
  (`Hemordna.Client.Support.TextSize`, delad med Installningar.razor som anropar samma helper
  direkt efter en lyckad sparning, så det egna fliken uppdateras utan omladdning). Global,
  medvetet - typsnittsskalning är per definition en hel-app-inställning (DESIGN.md §7), inte
  något en enskild sida kan äga.
- **"En uppgift åt gången"**: se ovan.

**Bugg hittad under obligatorisk skärmbildsgranskning, fixad:** svep-gestens
`element.setPointerCapture()` i `wwwroot/js/task-swipe.js` fångade pekaren redan vid
`pointerdown` var som helst i raden - inklusive ovanpå bock- och pil-knapparna - vilket helt
stoppade deras egna klick från att nå fram (`MinDagDetailTests`/`ExtraTaskTests` gick sönder på
just detta). Löst genom att `onPointerDown` genast returnerar om målet är eller ligger inuti en
`button`/`a`/`input`/`select`/`textarea` - ett svep kan bara starta på radens "tomma" yta.

**Tjuvkika på ett schema** flyttade oförändrad till `Planering.razor` (Vecka) - samma markup,
samma metoder (`OnPeekToggled`/`LoadPeekDayAsync`), bara i en annan fil. `PeekScheduleTests.cs`
uppdaterades att navigera till `/vecka` i stället för `/`.

**Kvarstående, dokumenterad begränsning:** `PlannedTaskResponse` bär ingen upprepningstext
("Varje dag", "Varje vecka") - meta-raden under uppgiftsnamnet visar därför bara "sedan
tidigare" när det gäller, aldrig hur ofta uppgiften återkommer. Ett nytt Api-fält löser det men
är utanför vad ett klient-bara steg får göra (CLAUDE.md: "Behöver du ett nytt API-fält: stanna
och rapportera").

### Steg 3 (`feat/ny-form-rum`) — `IMPLEMENTED`

Områden byggdes om till Rum enligt DESIGN.md §6: en bricka per rum (`RoomTile.razor`) i
stället för en 5000px hög sida med två knappar per uppgift, all redigering flyttad in i
`RoomSheet.razor`/`TaskOptionsSheet.razor` (`BottomSheet.razor`, byggd i steg 1, används nu
för första gången i skarpt läge). `RoomTasks.razor` - komponenten all uppgiftsredigering
tidigare låg i - är borttagen, helt ersatt.

**Api-undantag, explicit godkänt för detta enda syfte.** `TaskOptionsSheet`s fyra rader
(Upprepning/Vem gör det/Rum/Kräver vuxen) motsvarar fyra Application-anrop, men Api:t hade
bara ett av dem sen tidigare (`UpdateTaskFrequency`) - att ändra ansvarig, flytta en uppgift
till ett annat rum, eller ändra "kräver vuxen" gick bara att göra vid skapandet. Frågan
ställdes uttryckligen innan implementation (se konversationen) eftersom CLAUDE.md annars
säger stanna och rapportera för ett nytt Api-fält; svaret var att bygga alla fyra rader och
göra det nödvändiga tillägget i Application/Api, som en avsiktlig, dokumenterad avvikelse
från "ingen ändring i Domain/Application/Infrastructure/Api" - avgränsad till precis detta.

- **Domain krävde ingen ändring alls.** `TaskDefinition.AssignToArea`,
  `SetDefaultResponsibleMember`, `SetRotatingResponsibility`, `SetRequiresAdult` och
  `Area.Rename` fanns redan, oanvända av något use case.
- **Fyra nya, tunna Application-klasser** följer `UpdateTaskFrequency`s exakta mönster (hämta,
  mutera, `UpdateAsync`, `ArgumentException` för ett hushålls-främmande id):
  `Tasks.UpdateTaskAssignment` (`Guid? memberId` - null rensar ägaren och sätter rotation,
  ett satt id gör motsatsen; samma antingen/eller-konvention `Omraden.razor`s sovrums-ägare
  redan använde), `Tasks.MoveTaskToArea`, `Tasks.SetTaskRequiresAdult`,
  `Households.RenameArea`. 14 nya `Hemordna.Application.Tests`-tester (fakes, inga nya
  mönster).
- **Fyra nya, tunna Api-endpoints** i samma stil som `PUT .../frequency`: `PUT
  .../tasks/{id}/assignment`, `PUT .../tasks/{id}/area`, `PUT .../tasks/{id}/requires-adult`,
  `PUT .../areas/{id}/name` - request-DTO:er i `HouseholdContracts.cs`, registrerade i
  `Program.cs` som `AddScoped`.
- **Klienten** har egna kopior av anropen (`HemordnaApiClient`, anonyma JSON-objekt som
  request-kropp - samma mönster `UpdateTaskFrequencyAsync` redan använde, inga nya
  record-kontrakt behövda klientsidan).

**"Våning" är fortfarande bara en namnkonvention, inte ett domänfält.** Segmentkontrollen
(DESIGN.md §6) härleder våningarna genom att dela rumnamn på " – " (samma separator
`CreateFloorAsync` redan skrev in) - `Rum.razor.FloorOf`. Ett medvetet, dokumenterat
antagande: att lägga till ett riktigt `Floor`-fält hade varit ytterligare Api/Domain-arbete,
och bara "Byt namn" (nytt i detta steg) kan nu få ett rum att tappa sin våningsgruppering av
misstag genom att skriva över prefixet - det rummet hamnar då i "Annat" i stället för att
försvinna eller krascha något.

**"N idag"/"Nästa: veckodag" på varje `RoomTile` läser den inloggade medlemmens egen dag, inte
hela hushållets.** Ingen endpoint svarar på "vem i hushållet har vad idag, per rum" - att
fråga per medlem hade multiplicerat anropen nedan med antalet medlemmar. Lookahead-loopen (upp
till 7 dagar, samma tak och resonemang som Idags "Nästa: veckodag") körs EN gång för alla
rum tillsammans (`Rum.razor.LoadTodayAndNextAsync`), inte en gång per bricka - annars hade N
rum krävt N×7 anrop i stället för högst 8.

**Ombalanseringspanelerna flyttade oförändrade** - "Känns det som att en person gör för
mycket?" (`RebalanceTaskAssignments`) till Hushåll, "Ser fördelningen skev ut?"
(`RebalanceSchedule`) till Vecka - samma disclosure-markup och metoder, bara i en annan fil.
Ingen omdesign av själva panelerna; det är steg 4:s jobb för Hushåll/Vecka som helhet.

**Bugg hittad under obligatorisk skärmbildsgranskning, fixad:** `RoomSheet`s
"Ändra frekvens för hela rummet"-delvy återanvände texten "Stäng" för sin egen
"tillbaka till uppgiftslistan"-länk, vilket krockade med `BottomSheet`s alltid närvarande
egna "Stäng"-knapp i samma dialog (`GetByRole(Button, Name: "Stäng")` matchade två element).
Bytt till "Till uppgifterna".

### Steg 4 (`feat/ny-form-hushall-vecka`) — `IMPLEMENTED`

Hushåll byggdes om enligt DESIGN.md §6: en avatarrad (en knapp per medlem, ny `MemberSheet.razor`
äger roll/paus/borttagning) ersätter den gamla listraden per medlem. Vecka (döpt om från
`Planering.razor`) fick hushållets veckogrid som hjälte överst, med den inloggade medlemmens
egen "Min vecka" som ett `<h2>`-avsnitt under - samma innehåll och markup som tidigare, bara
omplacerat.

- **`MemberSheet.razor`** (ny, `BottomSheet`): rollval (samma tre förinställningar som
  `Support/HouseholdRole.cs` redan definierade), "Anpassad tid i stället" som en disclosure,
  paus för just den medlemmen, och "Ta bort medlem" som en destruktiv knapp längst ned. Ersätter
  den gamla inline-redigeringen (roll-`<select>`, egna paus-/ta bort-knappar) som låg direkt i
  `Hushall.razor`s medlemslista.
- **"Bjud in"** (ny `BottomSheet`) slår ihop två redan existerande, separata funktioner bakom en
  enda ingång: koden att dela (`Dela koden`, ny `wwwroot/js/share.js` - Web Share API där den
  finns, annars urklipp) och att lägga till en medlem utan eget konto, som nu ligger som en
  disclosure ("Eller lägg till en medlem utan eget konto") inuti samma ark i stället för ett
  eget, alltid synligt formulär på sidan.
- **"Områden"-kortet togs bort från Hushåll** - ett omdömesbeslut, inte en uttrycklig
  spec-punkt: Rum-fliken (steg 3) visar redan varje rums uppgiftsantal på sin egen bricka, så
  kortet dubblerade information utan att tillföra något Hushåll-specifikt.
- **"N våningar" i sidhuvudet** (`@_household.Areas.Count(...)` + `FloorCount`) delar
  våningsräkningen med Rum via en ny, liten `Support/RoomFloors.cs` (`FloorOf`/`CountDistinct`)
  - `Rum.razor` bytte sin egen privata `FloorOf`-metod mot samma statiska helper, i stället för
    att ha två kopior av samma " – "-parsning.
- **"Idag i hushållet"**: ett nytt, tyst kort med en ring (samma SVG-mönster som
  "Senaste händelser" redan använde) - hela hushållets andel klara uppgifter idag, aldrig per
  medlem (PRODUCT.md §8).
- **Ombalanseringspanelerna** ("Balansera om vem som gör vad", "Pausa hushållet") flyttade in i
  egna `BottomSheet`-ark i stället för `<details>`-utfällningar direkt på sidan - samma
  underliggande metoder (`RebalanceAssignmentsAsync`, `SaveHouseholdPauseAsync` m.fl.), bara ny
  container.

**Bugg hittad under obligatorisk skärmbildsgranskning, fixad:** `MemberSheet`s pausfält
(`Pausa till och med`) använde en `<div class="field"><span>...</span><input .../></div>` utan
någon `<label>`-koppling - till skillnad från hushållets egen pausruta på samma sida, som redan
använde `<label class="field">`. Fältet saknade därmed helt tillgängligt namn (bröt DESIGN.md
§10). Fixat genom att byta `<div>` mot `<label>`, samma mönster som redan fanns bredvid.

**Undersökt, inte en bugg:** en `FullPage: true`-skärmbild av Hushåll på mobil (390×844) visade
till synes navigationsraden överlappa "Inställningar"/"Logga ut" längst ned. Verifierat med en
riktig scroll till `document.documentElement.scrollHeight` (i stället för Playwrights egen
`scrollIntoViewIfNeeded`, som visade sig stanna för tidigt eftersom dess "redan synlig"-koll
inte känner till den fixerade navraden som täcker botten av viewporten) att `.app-main`s
`padding-bottom: 88px` (satt redan i steg 1) räcker med god marginal - artefakten kommer från
hur Chromiums `fullPage`-skärmbilder hanterar `position: fixed`-element på sidor högre än en
viewport, inte från appens egen layout.

**Testmönstret från steg 3** (skärmredaren "Stäng"/rader med samma text som knappen som öppnar
dem → skopa alltid till `page.GetByRole(Dialog, Name: "...")` innan interaktion inuti) upprepas
nu även för "Pausa hushållet" och "Balansera om vem som gör vad", vars listrad-, ark-titel- och
egen skicka-knappstext medvetet hölls identiska med tidigare UI-text. Ny delad testhjälpare,
`tests/Hemordna.E2E.Tests/HushallHelper.cs`
(`AddMemberWithoutAccountAsync`/`OpenMemberSheetAsync`), ersätter den upprepade
"öppna Bjud in-arket, fyll i det nästlade formuläret"-koden i fem olika testfiler.

### Steg 5 (`feat/ny-form-morkt-lage`) — `IMPLEMENTED`

Mörkt läge (docs/DESIGN.md §2/§10) - **ritat, inte inverterat**: varje mörk tokenvärde är sitt
eget övervägda val, kontrastverifierat på samma sätt som den ljusa paletten, inte en filter-
invertering. Applicerad två vägar, i `wwwroot/css/app.css`:
`@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {...} }` för automatiskt
mörkt läge, och `:root[data-theme="dark"]` för ett uttryckligt val som vinner oavsett OS.

- **`--gustav` är medvetet ORÖRD i mörkt läge.** Dess enda roll som bar token är
  bakgrund-under-vit-text (`.btn-primary`) - redan tema-oberoende AA-säker (5.50:1). Att göra
  den ljusare för läsbarhet mot en mörk yta hade brutit just den kopplingen. Överallt gustav
  behöver läsas som text/ikon mot en mörk yta används i stället `--gustav-ink` (redan den
  "textsäkra" varianten) - se buggen nedan.
- **Alla nya färgpar kontrastverifierade** med samma Python-script som den ljusa paletten
  (relativ luminans, sRGB-gammaformeln): `sot`/`kalk` 13.76:1, `gustav-ink`/`kalk` 7.22:1,
  vit/`gustav` 5.50:1, `sot`/`surface-2` 9.80:1, `ronn-ink`/`ronn-soft` 6.38:1,
  `gustav-ink`/`gustav-soft` 5.32:1, `sot`/`saffran-soft` 9.21:1, `aska`/`kalk` 5.39:1 (3:1-
  golvet för UI-komponenter som fokusringen, inte textens 4.5:1). Samtliga med god marginal.
- **`Support/Theme.cs` + `wwwroot/js/theme.js`**: ljust/mörkt/systemets eget, ett **per-enhet**
  val i `localStorage` (`hemordna.theme`) - inte `MemberPreference`, till skillnad från
  presentationsläget, eftersom rätt tema hör till skärmens egen miljö, inte till personen.
  `wwwroot/index.html` kör samma logik synkront, inline, innan `app.css` laddas, för att
  undvika en synlig blink av fel tema vid första målningen.
- **"Utseende"-kortet** på Inställningar (tre alternativknappar, applicerar direkt vid val -
  inget separat "Spara", till skillnad från presentationsläget ovanför som fortfarande sparas
  mot servern).
- **Bock- och svepbekräftelsen delas nu.** `task-swipe.js`s `confirm(element)` (flash +
  `navigator.vibrate(10)`, skippas helt - inklusive haptiken - under
  `prefers-reduced-motion`) fanns sedan tidigare bara för svepet; den vanliga
  bock-knappen (`TaskListItem.CompleteAsync`) anropar nu samma funktion innan den
  fullbordar uppgiften. CSS-klassen döptes om från `.task-swipe-confirm` till `.task-confirm`
  eftersom den inte längre är svep-specifik.

**Bugg hittad under arbetet, fixad:** `.btn-link:hover { color: var(--gustav); }` gav bara
2.98:1 mot mörkt lägets `--kalk` - bar `--gustav` är, som ovan, bara AA-säker som text i ljust
läge. Fixat med `filter: brightness(1.2)` relativt `--gustav-ink` (redan textfärgen i default-
läget) i stället för att byta till en annan token - fungerar i båda temana (5.34:1 ljust,
10.45:1 mörkt), och råkar dessutom ligga närmare den ursprungliga ljusa hover-tonen än den
gamla bar-`--gustav`-lösningen gjorde.

**Testat:** `tests/Hemordna.E2E.Tests/ThemeTests.cs` (4 tester - systemets eget, tvingat mörkt
med reload-persistens, tvingat ljust, tillbaka till systemets eget) väntar på attributet via
`page.WaitForFunctionAsync` snarare än att anta att `SetThemeAsync` hunnit slutföras direkt
efter `CheckAsync()` - den asynkrona Razor-hanterarens JS-import + `localStorage`-skrivning tar
en mätbar (om än kort) stund, vilket en enstaka omedelbar assert missade i en av fyra körningar
innan detta fixades. `SkarmbilderTests.Capture_dark_mode_screens` fångar Idag/Rum/Vecka/
Hushåll/Inställningar och tre ark (Nytt rum, MemberSheet, Extra uppgift) med
`page.EmulateMediaAsync(ColorScheme.Dark)`, granskade manuellt.

### Uppföljning efter driftsättning: redigera en uppgifts tid — `IMPLEMENTED`

Upptäckt i vanligt bruk efter att "Ny form" gått i produktion: `TaskOptionsSheet` (steg 3) hade
Upprepning/Vem gör det/Rum/Kräver vuxen, men ingen rad för tiden - uppskattad tid gick bara att
sätta vid skapandet. Samma mönster som steg 3:s Api-undantag: `TaskDefinition
.ChangeEstimatedMinutes` fanns redan i domänen, oanvänd av något use case. Explicit godkänt av
användaren innan implementation (samma "stanna och rapportera"-princip som CLAUDE.md kräver för
nya Api-fält).

- **`Application.Tasks.ChangeTaskEstimatedMinutes`** följer `SetTaskRequiresAdult`s exakta
  mönster. **`PUT .../tasks/{id}/estimated-minutes`** i samma stil som de fyra andra raderna.
- **Ny "Tid"-rad** i `TaskOptionsSheet`, mellan "Vem gör det" och "Rum" - samma
  `TimeLevel`-knappar som "Lägg till uppgift" redan använder, förvalda på närmaste nivå
  (`TimeLevel.ClosestMinutes`).
- Inget brott mot §6a: `TaskOptionsSheet` öppnas bara från Rum (planeringsläge), aldrig från
  Idag - samma undantag som redan gäller `RoomTile`/`RoomSheet`s egna minutsiffror.

**Uppföljning på uppföljningen, samma dag:** produktfeedback att skalan kändes fel - "Ingen tid"
borde faktiskt gå att spara (en uppgift som knappt tar någon tid alls är en rimlig, avsiktlig
beskrivning, inte ett oifyllt fält), och de tre andra nivåerna för höga. `TimeLevel.All` är:

| Nivå | Tidigare | Nu |
|---|---|---|
| Ingen tid | 0 min (gick inte att spara) | 0 min (giltigt val) |
| Lite tid | 15 min | 5 min |
| Lagom tid | 30 min | 15 min |
| Gott om tid → **Lång tid** | 60 min | 30 min |

`TimeLevel` är delad av `RoomSheet`/`TaskOptionsSheet` (uppgifters tid), `MemberSheet`/
`Hushall.razor` (en medlems anpassade veckotid, dag för dag) och `MinDag.razor`s "Extra
uppgift" - samma skala används överallt en tid väljs kvalitativt, medvetet, snarare än att
duplicera fyra separata skalor för fyra separata sammanhang.

Att göra 0 giltigt krävde att lätta på valideringen i tre lager, inte bara byta siffror i
klienten - annars hade "Ingen tid" fortfarande kastats ut:

- **Domain**: `TaskDefinition.Create`/`ChangeEstimatedMinutes` bytte `Guard.AgainstNonPositive`
  → `Guard.AgainstNegative` - samma guard `TaskAssignment`/`WeeklyTimeBudget`/
  `MemberAvailability` redan använde för minutfält, så definitionen blev konsekvent med resten
  av domänen snarare än en egen, strängare regel.
- **Api**: båda `<= 0`-valideringarna (skapa uppgift, ändra tid) bytte till `< 0` - felmeddelandet
  blev "Uppskattad tid kan inte vara negativ" i stället för "...måste vara större än noll".
- **Klient**: `RoomSheet.AddTaskAsync`/`TaskOptionsSheet.SaveTimeAsync`s egna
  "Välj ungefär hur mycket tid..."-spärrar togs bort - alla fyra knappvärden är nu giltiga, det
  finns inget kvarvarande ogiltigt läge att spärra mot.
- Ingen delning-med-noll-risk: sökt igenom Application/Client efter `EstimatedMinutes` -
  bara `Sum`/multiplikation någonstans (`DailyPlan`, `TaskWorkload`, `RebalanceTaskAssignments`
  m.fl.), aldrig division.

### Uppföljning: förenklad tjuvkik-lista med totaltid — `IMPLEMENTED`

Produktfeedback: "Tjuvkika på ett schema"s lista (Vecka) kändes rörig - varje rad visade namn,
en valfri områdeschip och ibland "sedan tidigare", men bara avklarade rader hade en egen
kryssruta/bock. Förenklat till namn + kryssruta för alla rader (tom cirkel för ej avklarat,
ifylld gustav-bock för avklarat - samma `.task-check`/`.task-check-done` som redan fanns för
de avklarade raderna), områdeschippen borttagen. "Sedan tidigare" behölls medvetet trots att
den strider mot "bara namn och kryssruta": utan den ser en gammal, fortfarande utestående
förekomst ut som en rak dubblett av morgondagens nya förekomst - en tidigare rapporterad,
riktig förvirring som `PeekScheduleTests
.Peeking_at_tomorrow_labels_todays_still_outstanding_occurrence_separately_from_tomorrows_own`
skyddar mot. Ingen fråga ställdes om just den här avvägningen; den är dokumenterad här i
stället, lätt att ändra om användaren ändå vill ha bort den.

**Ny "Totalt: N min"-rad** under listan (`DailyPlanResponse.PlannedMinutes +
CompletedMinutes`, redan beräknat serverside - ingen ny Api-yta). Ett uttryckligt, nytt
undantag från §6a (se DESIGN.md §6a) - att tjuvkika på en dag är, liksom Rum, en
planeringshandling ("hur full är den här dagen?"), inte den dagliga vyn själv.

Ingen ny testning för listans utseende krävdes utöver `PeekScheduleTests` (redan gröna,
oförändrade förväntningar på `.task`/"sedan tidigare"); en tillfällig skärmbild togs manuellt
för visuell granskning under arbetet, inte sparad som permanent test.

### Uppföljning: förenklad "Senaste händelser" — `IMPLEMENTED`

Produktfeedback (med skärmbild): varje rad i Hushålls "Senaste händelser" visade
"@MemberDisplayName markerade "@TaskName" som klar" plus ett klockslag - läst som brus, inte
användbar historik. Förenklat till samma kryssruta+namn-mönster som redan finns i Vecka/
`TaskListItem` (`.task`/`.task-check-done`), utan personens namn eller klockslaget - varje rad
här är redan per definition avklarad, så det finns aldrig en tom/ej avklarad variant att rita.
En egen, mindre (32px) kryssrings-cirkel i stället för den vanliga 44px: raderna är rent
informativa, inte tryckbara, så DESIGN.md §10:s 44px-krav på tryckytor gäller inte här, och en
lista som kan ha många rader (22 i den rapporterade skärmbilden) vinner på tätare rader.
`HushallActivityTests.Completing_a_task_shows_it_in_the_householders_recent_activity` bytte
`.list-item`/"Karin"-kontroll mot `.task`, utan någon assert på personens namn.

**Bugg hittad under felsökningen, fixad (ingen relation till listans utseende):**
`app.hemordna.se`s `service-worker.js`/`index.html` saknade ett eget `Cache-Control` helt -
en webbläsare kan då heuristiskt cacha svaret på egen hand, utan att någonsin fråga servern om
en ny version finns, även efter flera omstarter av appen (det faktiska, rapporterade symptomet:
tjuvkik-listans nya "Totalt: N min"-rad syntes inte trots omstarter, långt efter att den redan
låg i produktion). `_framework/`-tillgångarna är redan säkra att cacha för evigt (innehålls-
hashade filnamn - en ny build ger nya filnamn), men skalfilerna (`index.html`,
`service-worker.js`) behåller samma filnamn över varje driftsättning och måste omvalideras vid
varje besök för att en uppdatering ska bli synlig. Fixat med `StaticFileOptions
.OnPrepareResponse` i `Program.cs`: `Cache-Control: no-cache` för just de två filerna (tvingar
en villkorad GET, förbjuder inte cachning helt). En redan cachad webbläsare behöver fortfarande
en sista manuell cache-rensning/ominstallation för att komma loss - fixen gör bara att alla
framtida driftsättningar upptäcks pålitligt.

### Uppföljning: rumsgruppering hölls inte ihop per våning på Idag — `IMPLEMENTED`

Produktfeedback: "Beslut: Rumsgruppering på Min dag" (steg 1) grupperar redan uppgifter per
**rum**, men ett hushåll med flera våningar (`RoomFloors`, steg 3/4) fick sina rum utspridda i
`DailyPlanner`s egen, våningsblinda ordning - "Övre plan"s båda rum kunde hamna långt ifrån
varandra, med "Entré plan"s rum emellan.

- **`MinDag.razor.FloorGroups`** klustrar det redan beräknade `RoomGroups` ytterligare ett steg,
  efter `RoomFloors.FloorOf(item.AreaName)`. Samma först-förekomst-ordning som `RoomGroups`
  redan använde (se steg 1) - en våning eller ett rums plats i listan speglar fortfarande
  `DailyPlanner`s egen prioritering, aldrig en godtycklig sortering. Ett hushåll utan
  "Våning – "-namngivning alls samlas i en enda, våningslös klunga - en `<h2
  class="floor-heading">` renderas bara när det faktiskt finns fler än en våning bland dagens
  uppgifter, så ett vanligt enplanshushåll ser ingen skillnad.
- **`Support/RoomFloors.RoomNameOf`** (ny, parar med `FloorOf`) - rummets egna namn med
  "Våning – "-prefixet bortklippt. Används för rumsrubriken under en våningsrubrik ("Hall" i
  stället för "Övre plan – Hall", som annars upprepar våningsnamnet).
- **`TaskListItem` fick en ny `ShowAreaChip`-parameter** (samma mönster som redan fanns för
  `ShowOverdueNote`), `false` i den rumsgrupperade listan - produktfeedback, mitt i arbetet,
  att rummet annars stod på RADEN två gånger (rumsrubriken ovanför, och chippen på själva
  raden). Fortfarande `true` (förvalt) i "Sedan tidigare", den enda platsen en rad visas UTAN
  någon rumsrubrik ovanför sig - där är chippen fortfarande den enda platsen rummet står alls.
  `MinDagDetailTests.cs` uppdaterad till två tester (en per läge) i stället för ett, eftersom
  de nu förväntar sig motsatta saker.

Ny testfil `MinDagFloorGroupingTests.cs`: fyra rum på två våningar, skapade i medvetet
interfolierad ordning (Entré/Övre/Entré/Övre) - ett grönt test bevisar därför att våningarna
faktiskt klustras, inte att de råkar redan ligga i rätt ordning.

### Uppföljning: "Extra uppgift" erbjuder befintliga uppgifter — `IMPLEMENTED`

Produktfeedback: "Extra uppgift" var alltid ett tomt formulär - varje gång, oavsett om
hushållet redan hade en passande uppgift att bara plocka fram igen, skapades en helt ny
`TaskDefinition`. Löst helt klientsidigt i `MinDag.razor`, ingen ny Api-yta - `Api
.ListTasksAsync`/`GetHouseholdAsync`/`ScheduleOccurrenceAsync`/`SetAvailabilityAsync` fanns
redan sen tidigare steg.

- **`OpenExtraTaskSheetAsync`** laddar hushållets aktiva uppgifter (för rumsnamn) och filtrerar
  bort allt som redan är del av dagens plan (`Items`/`Completed`/`Unplanned`, matchat på
  `TaskDefinitionId`) - laddas om från grunden varje gång arket öppnas snarare än cachat, så en
  uppgift som lagts till någon annanstans sen sist syns direkt.
- **Listan är förstavalet**, en rad per befintlig kandidat (namn, kvalitativ tidsnivå - aldrig
  en rå minutsiffra, §6a gäller lika mycket här). En tryckning schemalägger direkt, ingen
  bekräftelse - samma "widen today's available time"-steg som fanns sen tidigare
  (`ScheduleForTodayAsync`, nu delad mellan båda vägarna) så uppgiften garanterat hamnar på
  dagens lista och inte tyst glider till "till en annan dag".
- **Uppföljning samma dag: listan grupperas per rum/våning** - produktfeedback att en platt
  lista blev lång och svårbläddrad så fort ett hushåll hade fler än en handfull kandidater.
  Exakt samma `RoomGroups`/`FloorGroups`-mönster som redan fanns för dagens egen lista, applicerat
  på `_extraTaskCandidates` i stället för `_day.Items` (`ExtraTaskRoomGroups`/
  `ExtraTaskFloorGroups`, samma `RoomFloors.FloorOf`/`RoomNameOf`). Eftersom rumsrubriken nu
  redan står ovanför varje rad togs den tidigare per-rad-chippen bort - samma resonemang som
  redan gällde för `TaskListItem.ShowAreaChip` i huvudlistan.
- **"Eller skriv en ny uppgift"** är en disclosure under listan med det oförändrade gamla
  formuläret - för en genuint ny engångssak. Har hushållet inga kandidater alls (ett färskt
  hushåll utan uppgifter) visas formuläret direkt, utan en tom lista och en meningslös
  disclosure runt den.

### Uppföljning: "Skriv en ny uppgift"-formuläret saknade rumsval — `IMPLEMENTED`

Bugg: `AddExtraTaskAsync` skickade alltid `AreaId: null` till `Api.CreateTaskAsync` - en
genuint ny uppgift (skriven via "Eller skriv en ny uppgift", inte plockad ur listan) kunde
därför aldrig hamna i ett rum, oavsett avsikt, och landade alltid under "Övrigt" på dagens
lista. Det andra flödet (plocka en befintlig kandidat, `AddExistingExtraTaskAsync`) var redan
korrekt - verifierat med ett nytt strikt E2E-test innan felsökningen smalnades av till just
skapa-formuläret (tre kandidater i tre olika rum, klick på den mittersta, verifierar rätt rum,
inga dubbletter).

- `ExtraTaskForm` fick ett `AreaId`-fält (`string`, inte `Guid?`) - samma konvention som
  `TaskOptionsSheet._roomAreaId` redan använder, eftersom ett `<select>`s `@bind` inte stödjer
  `Guid?` direkt.
- `NewExtraTaskForm` fick ett villkorligt rum-`<select>` (bara synligt när hushållet har minst
  ett aktivt område), mellan tidsnivå-väljaren och felmeddelandet.
- `AddExtraTaskAsync` parsar nu `_extraForm.AreaId` till `Guid?` vid inskick i stället för att
  hårdkoda `null`, och nollställer fältet efter en lyckad inskickning tillsammans med
  `Name`/`EstimatedMinutes`.
- Nytt E2E-test `Writing_a_brand_new_task_with_a_room_selected_lands_under_that_rooms_heading_on_idag`
  bevisar hela vägen: skapa uppgift med rum valt → hamnar under rätt rums rubrik på Idag, inte
  "Övrigt".

### Beslut: Ny form 2026 — `IMPLEMENTED` (alla sex delsteg, ej mergat till `main`)

Ett andra visuellt delta ovanpå "Ny form" (steg 1–5, ovan), på samma villkor: enbart
`Hemordna.Client` och dokumentation, ingen ändring i Domain/Application/Infrastructure/Api eller
i `DailyPlanner`s urval/ordning/tidsbudget, ingen `@code`-logik i sidorna byts ut. Sex fristående
delar, varsin egen commit på `feat/ny-form-2026`, mergas till `main` först efter uttryckligt
godkännande (samma regel som "Ny form" steg 1–5).

**Varför.** Lyfta klienten från "Ny form"s redan etablerade, lugna grund mot ett mer nutida
mobilt formspråk (mjukare ytor, svävande navigation, kant-till-kant-innehåll med progressiv
blur, scroll-medveten rubrik, ark med två höjdlägen, fjädrande bekräftelse) - utan att ge upp
DESIGN.md §10 (kontrast, 44×44px, färg aldrig ensam bärare, fokus, reduced motion) eller §6a
(tid döljs på Idag).

**Vad som medvetet INTE görs, i något av de sex stegen:** glas-transparens på innehållsytor
(kort, listor, ark) - bara på navigationspillen och de två tonade fälten, aldrig under AA-
kontrast för text ovanpå; dynamisk/adaptiv färg (t.ex. färg härledd från ett foto eller
användarval) - Gustaviansk blå/Saffran (DESIGN.md §2) är identiteten, inte en variabel; widgets
eller Live Activities - en PWA har ingen plattforms-API-yta för något av detta, och det är
under alla omständigheter utanför MVP-scope (CLAUDE.md §12/PRODUCT.md §10).

#### Delsteg 1 (punkt 5, "Squircle och kantlösa ytor") — `IMPLEMENTED`

- **Nya tokens** i `app.css`: `--radius` 14px → 22px, `--radius-sm` 10px → 14px, ny
  `--radius-xl` (26px, används först i delsteg 4:s ark). `--shadow-card` (ljust) från
  `0 1px 2px rgba(34,40,46,.06)` till `0 1px 0 rgba(34,40,46,.04)` - en tunnare, lägre skugga;
  mörkt läges egen skugga rörd inte.
- **Ny token `--edge`**: `transparent` i ljust läge (en vit yta läser redan mot `--kalk` utan
  en ritad linje), `var(--line)` i mörkt läge (där behövs en riktig kant för att skilja ytan
  från bakgrunden) - satt i `:root`, samt i båda mörka blocken
  (`@media (prefers-color-scheme: dark) :root:not([data-theme="light"])` och
  `:root[data-theme="dark"]`), samma tvåvägsmönster steg 5 redan etablerade.
- **`border: 1px solid var(--line))` → `var(--edge)`** på sex innehållsytor: `.card`/`.list`
  (app.css), `.task-list`/`.focus-card` (`MinDag.razor.css`), `.room-tile`
  (`RoomTile.razor.css`), samt `Hushall.razor.css`s egen `.task-list`-kopia ("Senaste
  händelser") - den sistnämnda utanför uppdragets uttryckliga selektorlista men en medveten
  utvidgning (uttryckligt godkänd innan implementation): utan den hade just det kortet varit
  det enda med synlig kant i ljust läge, en synlig inkonsekvens i skärmbilderna. Radskiljare
  INUTI listor (`.task`, `.list-item`) behåller `var(--line)` oförändrat - bara den yttre
  konturen mjukas upp.
- **`corner-shape: squircle`** (progressiv förbättring, inget fallback-behov) på samma sex
  selektorer plus `.sheet-shell` (`BottomSheet.razor.css`, bara egenskapen - dess
  `border-radius`-värde rörs i delsteg 4) och `.btn` (app.css).
- **Rört uttryckligen inte:** `.chip`/`.chip-today`/`.task-check`/`.avatar` - alla pill-formade
  (`--pill`, 999px), ingen del av "kantlösa ytor"-uppdraget. `.field input`/`.field select`
  (app.css) - formulärfält, inte innehållsytor, behåller `var(--line)`.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar); `Hemordna.Domain.Tests`
  82/82, `Hemordna.Application.Tests` 173/173, `Hemordna.E2E.Tests` 79/79 - alla oförändrade,
  ren CSS-ändring. Tolv skärmbilder (Idag/Rum/Hushåll × mobil 390×844/desktop 1280×900 ×
  ljust/mörkt) via ett tillfälligt `DEBUG_Capture_squircle_edges`-test, granskade manuellt och
  sedan borttagna igen: `.room-tile` (Kök/Övrigt) och Hushålls kort (Veckan/Idag i
  hushållet/Senaste händelser) läser helt kantlösa mot `--kalk` i ljust läge, tydligt men
  diskret avgränsade i mörkt läge; ingen överlappning, avklippt text eller horisontell scroll
  på någon av de sex sidvarianterna; desktop-railen (72px) oförändrad.

#### Delsteg 2 (punkt 1, "Flytande navigationspill") — `IMPLEMENTED`

- **Nya tokens** i `app.css`: `--glass` (ljust `rgba(255,255,255,.78)`, mörkt
  `rgba(28,34,38,.78)`), `--glass-edge` (ljust `rgba(255,255,255,.55)`, mörkt
  `rgba(255,255,255,.08)`), `--shadow-float` (ljust `0 6px 20px rgba(34,40,46,.16)`, mörkt
  `0 6px 20px rgba(0,0,0,.5)`) - samma `:root` + båda mörka block-mönster som `--edge`. Ny
  `.sr-only`-utility (klassiskt clip-mönster: `position:absolute; width/height:1px;
  clip:rect(0,0,0,0)` osv) - visuellt gömmer men behåller det tillgängliga namnet, till
  skillnad från `display:none` som hade tagit bort det helt.
- **`NavMenu.razor.css`s mobilblock** (`@media (max-width: 900px)`) skrevs om helt: `.nav-shell`
  flyter (`position: fixed; left: 50%; transform: translateX(-50%); bottom: max(14px,
  env(safe-area-inset-bottom))`), pill-formad (`border-radius: var(--pill)`), tonad
  `--glass`-bakgrund med `backdrop-filter: blur(18px) saturate(1.3)` (+ `-webkit-`-prefix) och
  `--shadow-float`, ingen `border-top` längre. `.nav-items` blev en rad med `flex: 0 0 auto`-
  barn (piller storleksätts efter innehåll, inte längre `flex:1` jämnt fördelat över hela
  bredden). `::deep .nav-link` är rad-layout, aktiv flik fylld gustav-bakgrund med vit text
  (samma "färg aldrig ensam bärare"-princip som desktop-railens `--gustav-soft`-tint, fast
  fylld i stället för tonad - en fylld pill behövde mer kontrast mot den halvgenomskinliga
  `.nav-shell`-bakgrunden bakom sig än en mjuk tint hade gett).
- **Bara den aktiva fliken visar text.** Inaktiva flikars `span.nav-label` göms med samma
  clip-mönster som `.sr-only` (skrivet direkt i den scopade regeln, eftersom en global
  utility-klass inte går att applicera på ett barn-element utan att duplicera Blazors egen
  `active`-matchning i C#) - `::deep .nav-link:not(.active) .nav-label`. Aldrig
  `display:none`: `MobileNavTests`/`SkarmbilderTests` hittar varje flik via dess tillgängliga
  namn (`GetByRole(Link, Name: "Rum")` m.fl.) oavsett vilken som råkar vara aktiv, och det
  hade slutat fungera annars.
- **Kompakt läge under scroll**: `::deep html[data-scrolled] .nav-link` (satt av
  `Support/ScrollState.cs`, se delsteg 4) krymper padding till `0 11px` och göms även den
  aktiva flikens text. `html` är inget NavMenu renderar själv, så `::deep` måste stå FÖRE
  `html[...]` i selektorn (inte bara före `.nav-link`) - annars hade Blazors CSS-isolering
  försökt lägga sitt scope-attribut på `html`, vilket aldrig matchar något. `:global(...)`
  övervägdes men är inte en verklig Blazor CSS-isolerings-funktion (bara `::deep` är) -
  verifierat genom att `::deep` redan var det enda mönstret resten av filen använde.
  `transition: padding .25s, background .2s`, avstängt under `prefers-reduced-motion` i ett
  nästlat `@media`-block (giltig CSS - "conditional group rules" får nästlas, ingen
  preprocessor inblandad).
- **`MainLayout.razor.css`** mobil: `.app-main`s `padding-bottom` 88px → 112px, så sista raden
  i en lång lista alltid scrollar helt fri från pillens egen ruta snarare än att bara stanna
  ovanför var den gamla fasta raden brukade börja.
- **Desktop-railen (≥ 901px) rörd inte** - `@media (max-width: 900px)` omsluter hela
  ändringen.
- **Kontrast, uppmätt mot den faktiskt renderade bakgrunden (inte token-värdet)**: `--glass`
  över `--kalk` ger ≈ `rgb(253,252,251)` ljust och ≈ `rgb(28,34,38)` mörkt (alfakomposition,
  sRGB). Inaktiva flikars `--sot-soft`-ikonfärg mot den bakgrunden: **5.73:1 ljust, 7.34:1
  mörkt** - båda långt över både 3:1-golvet för UI-komponenter/ikoner och 4.5:1 för text.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar); `Hemordna.Domain.Tests`
  82/82, `Hemordna.Application.Tests` 173/173, `Hemordna.E2E.Tests` 80/80 (79 tidigare + det nya
  testet nedan). Nytt test `MobileNavTests.Scrolling_to_the_bottom_clears_the_floating_pill` (8 uppgifter seedade
  via Api, scrollar till botten, verifierar att sista `.task`s `BoundingBox` ligger helt ovanför
  `nav.nav-shell`s). Tre skärmbilder (Idag/Rum ljust, Idag mörkt, mobil 390×844) via ett
  tillfälligt `DEBUG_Capture_floating_pill`-test, granskade och borttagna: piller flyter fritt
  med synlig bakgrund runt om, bara "Idag" visar text i en fylld gustav-pill, övriga tre bara
  ikon, ingen hård kant mot botten.

#### Delsteg 3 (punkt 2, "Kant-till-kant med progressiv blur") — `IMPLEMENTED`

- **`index.html`**: viewportens `content` fick `viewport-fit=cover` - krävs för att
  `env(safe-area-inset-*)` ska returnera ett verkligt värde på en iPhone med hemknapp-indikator/
  hack, i stället för `0px`.
- **`MainLayout.razor`**: två nya `<div aria-hidden="true">` (`.app-topfade`/`.app-botfade`)
  direkt i `.app-shell`, före respektive efter `<main>` - fasta, icke-interaktiva
  (`pointer-events: none`) toningsremsor, bara synliga ≤ 900px (`display:none` på desktop).
  `.app-main`s eget innehåll scrollar UNDER dem; de rör sig aldrig själva.
- **`.app-topfade`**: `position: fixed; top:0; height: calc(44px + env(safe-area-inset-top))`,
  `linear-gradient(var(--kalk) 35%, transparent)` + `backdrop-filter: blur(10px)` (+
  `-webkit-`) maskerad med en spegelvänd `mask-image`-gradient så själva blur-effekten också
  tonar ut i stället för att sluta med en egen hård kant. `z-index: 9` - under piller (10) och
  ark (20/21), över det vanliga sidinnehållet.
- **`.app-botfade`**: samma mönster spegelvänt (`to top`), `bottom:0; height:110px` - täcker
  ungefär pillens egen zon plus lite marginal, så sista raden tonar innan den försvinner bakom
  piller i stället för att klippas tvärt.
- **`.app-main`** mobil: `padding-top` fick `+ env(safe-area-inset-top)` (var bara
  `var(--space-4)`) - annars hade `.app-topfade`s nya, säkerhetszon-medvetna höjd kunnat täcka
  början av innehållet på en enhet med hack.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar); `Hemordna.Domain.Tests`
  82/82, `Hemordna.Application.Tests` 173/173, `Hemordna.E2E.Tests` 80/80 (en känd,
  fördokumenterad flakighet - `HouseholdInviteTests.Joining_with_a_valid_code_...` - föll under
  full parallell körning, passerade isolerat, och en fullständig omkörning gav 81/81 rent,
  exakt det mönster CLAUDE.md/uppdraget beskriver som pre-existing snarare än en verklig regression).
  Fyra viewport-beskurna (inte `FullPage`, som stitchar hela sidan till en komposit och därför
  inte visar fasta elements verkliga relation till den AKTUELLA viewporten) skärmbilder via ett
  tillfälligt `DEBUG_Capture_edge_to_edge_fades`-test (tio uppgifter seedade, scrollad till en
  punkt där en rad faktiskt ligger under toppremsan, och till botten där de sista raderna ligger
  under piller/bottenremsan), granskade och borttagna: en mjuk, rundad vinjettering syns vid
  kortens hörn precis där blur-zonen möter kortets skarpa kant - tydligast i mörkt läge - i
  stället för en hård linje; ingen horisontell scroll eller överlappning.

#### Delsteg 4 (punkt 3, "Scroll-driven rubrik") — `IMPLEMENTED`

- **Ny `wwwroot/js/scroll-state.js`** + **`Support/ScrollState.cs`**: en enda,
  rAF-strypt (`requestAnimationFrame`) passiv `scroll`-lyssnare som sätter/tar bort
  `document.documentElement.dataset.scrolled` vid tröskeln 24px - attacherad EN gång från
  `MainLayout.OnAfterRenderAsync(firstRender)`, ingen per-sida-JS. `animation-timeline:
  scroll()` hade löst samma sak i ren CSS men saknar stöd i Firefox, därför attributet.
- **Avvikelse, uttryckligen godkänd innan implementation.** Specen ville visa
  "N av M klara · N min kvar" i den kollapsade raden - det bryter mot DESIGN.md §6a, ett
  tidigare uttryckligen bekräftat, hårt beslut (se steg 2, "Tid förblir dold, tvärtemot
  konceptets egen skiss") att Idag aldrig visar minuter, inte ens summerat. Frågan ställdes
  innan kod skrevs; svaret var att behålla §6a orört - den kollapsade raden visar bara
  "N av M klara", samma kvalitativa text som redan står i den fulla rubriken.
- **`MinDag.razor`**: `p.day-date`/`h1`/`p.day-counts`/`div.progress` samlade i
  `<header class="day-header">`; `<div class="day-header-collapsed" aria-hidden="true">`
  (bara `<strong>Idag</strong>` + `<span>N av M klara</span>`) direkt före. `h1` kvar i DOM
  oförändrad (`SignUpHelper`/`MinDagTests` beror på den). `Hushall.razor` fick samma mönster
  (`<strong>@_household.Name</strong>` ensam i den kollapsade raden - hushållets sammanfattning
  har ingen kvalitativ "N av M"-motsvarighet). `h1` på Idag höjdes till
  `calc(var(--font-size-base) * 2.1)`, `letter-spacing: -0.025em`, `line-height: 1.02` - ingen
  `white-space: nowrap` fanns att ta bort (grep bekräftade att ingen sådan regel någonsin
  funnits för `h1`).
- **Bugg hittad under obligatorisk skärmbildsgranskning, fixad:** den kollapsade raden var helt
  osynlig - `z-index: 8` (som specen angav) medan `.app-topfade` (steg 3) ligger på `z-index: 9`
  med en gradient som är HELT OPAK `--kalk` de första 35% av sin egen höjd. Raden hamnade bakom
  en solid yta, inte bara blurrad. Fixat genom att lyfta `.day-header-collapsed`/
  `.household-header-collapsed` till samma `z-index: 9` som toppremsan - eftersom den kollapsade
  raden renderas SENARE i DOM:en (inuti `<main>`, en syskon-`<div>` efter `.app-topfade`), vinner
  den den vanliga "senare i dokumentordning vid lika z-index"-regeln och målas ovanpå, precis som
  en riktig iOS-navigationsrad ritar sin egen titel ovanpå den blurrade baren i stället för att
  blurras av den.
- **Andra buggen hittad under samma granskning, fixad:** i "Stor text"-läget (större bastext)
  var "TISDAG 8 SEPTEMBER" halvt osynlig REDAN I VILOLÄGE (ingen scroll alls) - `.app-main`s
  mobila `padding-top` (bara `var(--space-4)`, 16px) var kortare än `.app-topfade`s egen höjd
  (44px), så den första raden alltid låg delvis inuti blur-/gradientzonen. Fixat genom att höja
  `padding-top` till `calc(44px + env(safe-area-inset-top) + var(--space-2))` - ett litet
  `MainLayout.razor.css`-fel som spårar tillbaka till steg 3 men upptäcktes och fixas här, samma
  mönster som tidigare steg (t.ex. steg 1 → löst i steg 2).
- **Nytt permanent test** `ScrollHeaderTests.Idags_collapsed_header_only_becomes_visible_after_scrolling`
  - verifierar `getComputedStyle(...).opacity`, INTE Playwrights egen `ToBeVisibleAsync`
    (som varken bryr sig om `opacity` eller kunnat upptäcka den första buggen ovan - en ren
    stacking-defekt). Två testbuggar hittades och fixades under skrivandet: för lite seedat
    innehåll (en enda uppgift räckte inte för att sidan skulle bli scrollbar alls - tio
    uppgifter används nu, samma mönster som skärmbildstesterna) och en race mot den 0.2s CSS-
    övergången (väntar nu på det faktiska beräknade opacity-värdet, inte bara attributet som
    triggar det).
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar); `Hemordna.Domain.Tests`
  82/82, `Hemordna.Application.Tests` 173/173, `Hemordna.E2E.Tests` 81/81. Skärmbilder (Idag/
  Hushåll, ljust/mörkt, scrollad/ej scrollad) via ett tillfälligt `DEBUG_Capture_scroll_header`-
  test samt ett separat `DEBUG_Capture_large_text_h1`-test (Stor text-läge, 390px), granskade
  och borttagna: den kollapsade raden syns tydligt och läsbart först efter scroll i båda teman,
  h1 klipps inte och radbryter inte i Stor text vid 390px, "TISDAG ..."-etiketten är helt skarp
  i viloläge efter padding-fixen.

#### Delsteg 5 (punkt 4, "Ark med två lägen") — `IMPLEMENTED`

- **Ny `Components/SheetDetent.cs`** (`enum SheetDetent { Half, Full }`) + ny `[Parameter]
  Detent` på `BottomSheet.razor` (förvalt `Full`, oförändrat beteende för allt som inte
  uttryckligen sätter `Half`). `data-detent="half|full"` renderas på `.sheet-shell`.
- **`BottomSheet.razor.css`**: `.sheet-shell[data-detent="half"] { max-height: 56vh }` (mobil
  bara - desktopdialogen, `@media (min-width:640px)`, återställer uttryckligen 80vh för BÅDA
  attributvärdena, eftersom attributselektorn annars vinner över den vanliga `.sheet-shell`-
  regeln på specificitet oavsett `@media`-block, och skulle annars läcka in 56vh på desktop
  också). `.sheet-scrim` fick `backdrop-filter: blur(3px)`. Radie höjd till `var(--radius-xl)`
  (26px) på båda övre hörnen.
- **Drag i `wwwroot/js/bottom-sheet.js`** (ny `attachDrag`): pekar-events på BÅDE `.sheet-handle`
  och `.sheet-header` (samma `setPointerCapture`-mönster som `task-swipe.js` redan använder).
  Drag uppåt > 60px anropar `ExpandAsync` (Blazor-sidan sätter `Detent = Full`); drag nedåt >
  80px anropar `DismissAsync` (samma `CloseAsync`-väg som "Stäng"-knappen). Visuell
  `translateY`, klampad 0..120 (aldrig negativ - ett uppåtdrag är bara en gest, ingen visuell
  förflyttning, eftersom "half" redan visar arket i viloläge). Hoppar över `transform` under
  `prefers-reduced-motion` (samma teknik som `task-swipe.js`), men behåller själva
  tröskellogiken - matchar det etablerade mönstret i `task-swipe.js`s egen
  reduced-motion-hantering.
- **Bugg hittad under egen testskrivning, fixad:** ett nyskapat `RoomSheet` (ingen `Detent`
  satt, ska förvalt bli `Full`) öppnades med `data-detent="half"`. Orsak: `SheetDetent`s första
  medlem (`Half`) har det numeriska värdet `0` - samma värde som ett ofyllt `private SheetDetent
  _detent`-fälts eget default. Den ursprungliga koden synkade bara `_detent = Detent` inuti
  `OnAfterRenderAsync`, som körs EFTER den första renderingen som redan visar arket - den
  renderingen använde alltså fältets kvarvarande default (`Half`) i stället för den satta
  parametern. Fixat genom att flytta synkroniseringen till `OnParametersSet` (körs FÖRE
  rendering), med en egen `_detentWasOpen`-flagga skild från `_wasOpen` (som `OnAfterRenderAsync`
  fortfarande äger för sin egen JS-interop-timing) - ett nytt test,
  `SheetDetentTests.TaskOptionsSheet_opens_half_and_RoomSheet_opens_full_and_Esc_closes_each`,
  fångade buggen direkt.
- **`Detent="SheetDetent.Half"` satt på:** `TaskOptionsSheet` (dess egen `BottomSheet`),
  `MemberSheet`, Hushålls "Bjud in"/"Pausa hushållet"/"Balansera om vem som gör vad". **`Full`
  (förvalt, ingen ändring):** `RoomSheet`, "Nytt rum", "Extra uppgift", "Flytta till en annan
  dag", "Lägg till uppgift i …".
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar); `Hemordna.Domain.Tests`
  82/82, `Hemordna.Application.Tests` 173/173, `Hemordna.E2E.Tests` 82/82 (81 tidigare + det nya
  `SheetDetentTests` nedan). Sex skärmbilder
  (`RoomSheet`=full/`TaskOptionsSheet`=half ljust+mörkt, "Pausa hushållet"/"Balansera om vem som
  gör vad"/"Bjud in" i halvt läge mörkt) via ett tillfälligt `DEBUG_Capture_sheet_detents`-test,
  granskade och borttagna: `TaskOptionsSheet` visar `RoomSheet`s brickor/rader synliga bakom sig
  genom scrimmets blur, inget innehåll klipps i något av de tre halva Hushålls-arken - allt
  ryms inom 56vh eller scrollar internt.

#### Delsteg 6 (punkt 6, "Fjädrande bekräftelse") — `IMPLEMENTED`

- **`Components/TaskListItem.razor.css`**: `.task-confirm` bytte från en ren
  `background-color`-övergång till en `@keyframes task-spring`-animation (`0% scale(1)` →
  `40% scale(1.025)` + `background: var(--gustav-soft)` → `100% scale(1)`,
  `animation: task-spring .32s cubic-bezier(.2, 1.4, .4, 1)`) - en mjuk, studsande bekräftelse
  i stället för en platt färgflash.
- **`wwwroot/js/task-swipe.js`**: timeouten som tar bort `.task-confirm` höjd från 220ms till
  340ms, så klassen aldrig hinner plockas bort mitt i animationens egna .32s.
- **`navigator.vibrate(10)` kvar oförändrad**, men nu uttryckligen dokumenterad (DESIGN.md §4a):
  iOS Safari ignorerar `navigator.vibrate` helt och tyst - det är därför aldrig beskrivet i
  produkttext som en funktion appen har, bara ett bästa-möjliga tillägg på plattformar som
  faktiskt stödjer det.
- **`prefers-reduced-motion`**: `confirm()` returnerade redan tidigt innan denna klass någonsin
  sätts - verifierat (se nedan), inget ytterligare att stänga av.
- **Verifierat, med ett skript snarare än en skärmbild** (en animation syns inte i en stillbild):
  ett tillfälligt test anropade `task-swipe.js`s `confirm()` direkt på en riktig, redan
  renderad `.task`-rad (inte ett syntetiskt `document.createElement`-element - Blazors
  CSS-isolering stämplar bara verkligt renderade element med sitt scope-attribut, så en
  konstruerad `<div>` hade aldrig matchat den scopade `.task-confirm`-regeln). Bekräftade att
  `getComputedStyle(el).animationName` normalt börjar med `task-spring` (Blazors
  CSS-isolering döper om även `@keyframes`-identifierare, inte bara selektorer, till
  `task-spring-b-xxxxxxxx` - förväntat, inte en bugg) och att `.task-confirm` ALDRIG läggs till
  under `ReducedMotion.Reduce`. Testet togs bort igen efter verifiering, samma
  tillfälliga-test-mönster som skärmbilderna genom hela detta uppdrag - den ursprungliga
  bock-/svep-bekräftelsen (steg 5) har heller aldrig haft ett eget permanent E2E-test av samma
  skäl (skärmbildsgranskning i stället), så inget nytt permanent test lades till här.
- `dotnet build Hemordna.slnx` (0 fel/varningar); `Hemordna.Domain.Tests` 82/82,
  `Hemordna.Application.Tests` 173/173, `Hemordna.E2E.Tests` 82/82 (en enskild,
  orelaterad flakighet - `PasswordResetTests.Following_the_reset_link_...`, rör
  lösenordsåterställning, inget den här commiten rör - föll under full parallell körning,
  passerade isolerat, ny fullständig körning gav 82/82 rent).

### Sammanfattning

Alla sex delsteg av "Ny form 2026" mergades till `main` (`3531eb2`) med uttryckligt
godkännande och kör i produktion.

### Beslut: Ångra och stabil lista — `IMPLEMENTED`

NPF-revisionens åtgärder (`feat/npf-revision`) - en granskning av kognitiv tillgänglighet
(ADHD, autism, språkstörning, IF) fann att grunden är rätt (skuldfritt språk, fokusläge,
rumsgruppering) men att några MEKANISMER motverkar den: en avbockning går inte att ångra, en
realtidsuppdatering ritar om hela listan mitt i en interaktion, "Lugn" sparas men syns aldrig,
uppskattad tid är antingen helt dold eller en rå minutsiffra. Detta är inget nytt formsteg -
inget i `DailyPlanner`s urval/ordning eller i det visuella uttrycket från "Ny form"/"Ny form
2026" ändras. **Inget ord om diagnoser, funktionsnedsättning eller "tillgänglighet" förekommer
i UI:t** - varje val beskrivs av vad det gör, aldrig av vem det är för; se docs/PRODUCT.md §7.

**Del C, den bärande regeln för hela uppdraget:** ett hushåll är normalt blandat - en medlem
kan ha valt "Steg för steg", en annan inget alls. Allt i detta uppdrag är antingen per medlem
(`MemberPreference`) eller per enhet (`localStorage`), aldrig på `Household`. Delade ytor
(Hushåll, Vecka, Rum) visar aldrig vilket läge någon valt.

#### A1 (Ångra avbockning) — `IMPLEMENTED`

- **`TaskOccurrence.Reopen(Guid byMemberId, DateTimeOffset now)`** (Domain): kastar
  `DomainException` om occurrensen inte är `Completed`, om `byMemberId` inte är samma person
  som `CompletedByMemberId`, eller om `now - CompletedAt > 15 minuter`. Annars: `Status` →
  `Planned`, `CompletedByMemberId`/`CompletedAt` → `null` - exakt samma fält `Complete` satte,
  nollställda, inte en ny "ångrad"-status. **Varför 15 minuter och bara samma person:** ett
  felslag ska gå att ta tillbaka snabbt av den som gjorde det - inte en historik någon annan
  kan skriva om i efterhand. Ett hushåll är blandat (Del C) - den som ångrar sin egen
  avbockning ska inte behöva förklara sig, och ingen annan ska kunna ångra åt någon.
- **`Application/Tasks/ReopenTaskOccurrence.cs`** speglar `CompleteTaskOccurrence` exakt
  (samma tre beroenden, samma `TimeProvider`-mönster - CLAUDE.md §5). Returnerar `bool?`
  (`null` = occurrensen finns inte i hushållet); ett regelbrott (fel person, utanför
  fönstret, redan utestående) kastar `DomainException` och fångas INTE här - samma stil som
  `Complete`/`Defer` redan har, så anroparen får ett riktigt 409 via `DomainExceptionHandler`,
  inte ett tyst `false`.
- **`POST /api/households/{householdId}/occurrences/{occurrenceId}/reopen`** bredvid
  `/complete`, `memberId` från samma `httpContext.GetMembership()` som `/complete` redan
  använder - anroparen kan bara ångra som sig själv. `204 No Content` på lyckad ångring
  (inget kroppsinnehåll att returnera - Application-lagret ger bara `bool?`), `404` om
  occurrensen inte finns, `409` vid regelbrott.
- **Klient**: `HemordnaApiClient.ReopenOccurrenceAsync(householdId, occurrenceId, ct) → bool`,
  samma form som `CompleteOccurrenceAsync`. Inte kopplad till något UI ännu - det är B1.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87 (82 tidigare + 5 nya: ångra inom fönstret, fel person kastar, efter 15 min kastar,
  icke-`Completed` kastar, ångra-sedan-bocka-av-igen fungerar). `Hemordna.Application.Tests`
  177/177 (173 tidigare + 4 nya, `ReopenTaskOccurrenceTests.cs`, samma fejk-mönster som
  `CompleteAndDeferTests.cs`: notifierar exakt en gång, okänd occurrence → `null`, fel
  person/utanför fönstret → kastar och skriver ingenting). API:t verifierat riktigt körande
  (CLAUDE.md §7): ett tillfälligt `DEBUG_ReopenApiTests`-test (två riktiga konton i samma
  hushåll via den faktiska inbjudningskod-vägen) anropade den skarpa, körande endpointen -
  ångra före avbockning → riktigt 409, fel persons riktiga HTTP-anrop → riktigt 409, rätt
  person → riktigt 204 och occurrensen syns åter bland dagens utestående via ett riktigt
  `GET .../plan`-anrop. Testet togs bort igen efter verifiering; den permanenta,
  produktnära täckningen är B1:s `UndoTests` (riktigt UI-flöde) och C:s
  `MixedHouseholdTests`, som läggs till senare i den här grenen.

#### A2 (Preferensfält för tid) — `IMPLEMENTED`

- **`MemberPreference.ShowTimeLevel`** (bool, förvalt `false`) + `ChangeShowTimeLevel(bool)` -
  samma mönster som `Presentation`/`Motivation` redan har. Per medlem, inte per hushåll (Del
  C) - precis som resten av `MemberPreference`.
- **Migration `AddShowTimeLevelToMemberPreference`**: en enda additiv `AddColumn<bool>` med
  `defaultValue: false`, ingen `Down` som förlorar data utöver att ta bort kolumnen igen. Läst
  innan applicering; applicerad i dev (`dotnet ef database update`), verifierad
  (`ALTER TABLE "MemberPreferences" ADD "ShowTimeLevel" boolean NOT NULL DEFAULT FALSE`).
- **`SetMemberPreference.HandleAsync`** fick parametern `bool showTimeLevel` - alla tre
  anropsställen (Api-endpointen, `DevelopmentDataSeeder`, testerna) uppdaterade i samma
  commit så lösningen bygger genomgående.
- **`PreferenceResponse`/`SetPreferenceRequest`** (Api och klient) fick `ShowTimeLevel`.
  Saknas fältet i en `PUT`-kropp (en äldre klient) blir det `false` automatiskt - System.Text
  .Jsons vanliga beteende för ett obligatoriskt `bool` utan JSON-motsvarighet, ingen särskild
  hantering behövd.
- **`Installningar.razor`** fick ett `_showTimeLevel`-fält som läses/skickas med vid `Spara`,
  men INGEN ny kontroll än - bara plumbing så en sparning av `Presentation`/`Motivation` inte
  av misstag nollställer ett värde satt via en framtida kontroll. Den faktiska kryssrutan är
  B11:s jobb.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). Migrationen genererad, läst
  och applicerad i dev enligt ovan. `Hemordna.Domain.Tests` 87/87 (oförändrat - inga nya
  domänregler, bara ett fält). `Hemordna.Application.Tests` 177/177 (befintliga
  preferenstester utökade med `ShowTimeLevel`-assertioner i stället för nya testmetoder:
  sparas och läses tillbaka, uppdateras vid en andra sparning, defaultar till `false`).
  `Hemordna.E2E.Tests` 82/82 rent - kört i sin helhet eftersom commiten rör klientkod
  (`Installningar.razor`, `HemordnaApiClient`, delade kontrakt), inte bara backend.

#### B8 (Lugnare skärm) — `IMPLEMENTED`

- **`Support/CalmScreen.cs` + `wwwroot/js/calm-screen.js`** speglar `Theme.cs`/`theme.js`
  exakt: `localStorage`-nyckel `hemordna.calm` (`"1"`/saknas), attribut `data-calm` på
  `<html>`, samma synkrona inline-snutt i `index.html` (utökad, inte duplicerad) så det gäller
  innan Blazor och `app.css` hinner måla något. Per enhet, inte per medlem (Del C) - exakt
  samma motivering som temat redan har: det är en egenskap hos skärmen man håller i.
- **Global neutraliserande regel** i `app.css`, `html[data-calm] * { animation-duration:
  .001ms!important; ... }` - en ordagrann kopia av det redan befintliga
  `prefers-reduced-motion`-blocket, bara nyckla på attributet i stället för media-frågan. Detta
  ensamt räcker för `.task-confirm`s fjädring och `.progress > i`s övergång - ingen egen regel
  behövdes för någon av dem, eftersom båda bara är vanliga `animation-`/`transition-duration`-
  värden som den generella regeln redan fångar.
- **Tre statiska, riktade overrides** för sådant den generella regeln INTE når (genomskinlighet
  är inte en varaktighet): `NavMenu.razor.css` (`.nav-shell` → `var(--surface)`, ingen
  `backdrop-filter`), `MainLayout.razor.css` (`.app-topfade`/`.app-botfade` → solid `--kalk`
  med en `var(--line)`-kant - INTE `--edge`, som medvetet är genomskinlig i ljust läge; utan en
  alltid synlig linje hade "hård kant" varit osynlig exakt där den behövs, eftersom remsans
  bakgrund annars är identisk med sidans egen), `BottomSheet.razor.css` (`.sheet-scrim` utan
  blur, mörkläggningen kvar - arket är fortfarande modalt).
- **`task-swipe.js`**: `reduceMotion`-flaggan (redan avläst en gång per `attach()`) blir
  `prefers-reduced-motion ELLER data-calm` - svepets dragrörelse är en "rörelse" oavsett källa.
  Samma ögonblicksbilds-begränsning som `prefers-reduced-motion` redan har (ändras inte live
  för en redan fäst rad, bara nästa gång en lista laddas om) - medvetet, inte en ny svaghet.
- **Medveten avgränsning, inte en spec-avvikelse jag ändrat på eget initiativ:** `wwwroot/js/
  bottom-sheet.js`s egen drag-till-expandera/stäng-gest (`attachDrag`, "Ny form 2026" steg 5)
  har KVAR bara sin egen `prefers-reduced-motion`-koll, ingen `data-calm`-koll - B8:s filuppsättning
  namnger uttryckligen `task-swipe.js`, inte `bottom-sheet.js`. En riktig, om än liten,
  produktinkonsekvens (ett halvt arks drag skulle fortfarande animeras under "Lugnare skärm"),
  flaggad här snarare än tyst utökad utanför den angivna filuppsättningen.
- **Ingen kontroll i UI:t ännu** - `data-calm` går bara att sätta via `localStorage` direkt
  fram till B11.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ingen ändring - ren klientfunktion).
  `Hemordna.E2E.Tests` 82/82 rent. Eftersom ingen UI-kontroll finns än verifierades den
  faktiska effekten med ett tillfälligt `DEBUG_CalmScreenTests`-test (satte `localStorage`
  direkt, precis som den riktiga knappen kommer göra, och laddade om) - bekräftade att
  navpillen blir solid `--surface` utan `backdrop-filter`, att `.app-topfade` blir solid
  `--kalk` utan blur/mask och med en riktig 1px-kant, och att `.progress > i`s övergångstid
  faller till den neutraliserande regelns `0.001ms` (`getComputedStyle` rapporterar det som
  `1e-06s` - sekunder, inte millisekunder, samma tal). Testet togs bort igen efter
  verifiering.

#### B9 (Navigation: namnen alltid synliga) — `IMPLEMENTED`

- **`NavMenu.razor.css`**: sr-only-reglerna för `.nav-link:not(.active) .nav-label` och för
  `html[data-scrolled] .nav-link .nav-label` borttagna. Alla fyra namn syns nu alltid, oavsett
  vilken flik som är aktiv eller om sidan är scrollad. Ny `::deep .nav-label`-regel ger
  etiketterna en egen, mindre bas-storlek (`.72`) skild från länkens egen (`.85`, som
  fortfarande styr ikonens avstånd) - fyra alltid synliga namn behöver läsas som kompakta
  etiketter, inte fyra knappars fullstora text. Kompakt läge under scroll krymper vidare till
  `.62` med `padding: 0 9px` (var `0 11px`).
- **Verklig bugg hittad och fixad, som spårar tillbaka till "Ny form 2026" steg 1 (inte ny i
  det här steget):** den redan existerande kompakt-läges-regeln `::deep html[data-scrolled]
  .nav-link { padding: 0 9px; }` (tidigare `0 11px`) hade ALDRIG haft någon effekt alls sen den
  skrevs - Blazors CSS-isolering sätter in scope-kontrollen OMEDELBART efter `::deep`, så
  `::deep html[data-scrolled] .nav-link` kompileras till `[scope] html[data-scrolled]
  .nav-link` - ett krav att något med DENNA komponents scope ska vara en ANFADER till
  `<html>`, vilket aldrig kan stämma (`<html>` har inga anfäder). Upptäckt genom att läsa den
  faktiska kompilerade selektorn i `obj/…/scopedcss/bundle/Hemordna.Client.styles.css`, inte
  genom att resonera om källkoden - ett nytt försök att lägga till motsvarande regel för Stor
  text (se nedan) gav exakt samma symptom (mätvärdet ändrades inte alls efter ändringen),
  vilket avslöjade att mönstret redan var trasigt. Fixat genom att styra scope-kontrollen genom
  `.nav-shell` (komponentens eget rotelement, en riktig ättling till `html` OCH en riktig
  anfader till `.nav-link`): `html[data-scrolled] .nav-shell ::deep .nav-link` - `::deep`
  scopear allt FÖRE sig självt normalt (`.nav-shell` får scope-attributet), och lämnar allt
  EFTER sig obehandlat (`.nav-link`), topologiskt möjligt. Samma mönster användes för Stor
  text-regeln nedan direkt, i stället för att upprepa misstaget.
- **Stor text (DESIGN.md §7, §10 "Stor text får inte bryta layouten")**: vid 390px och
  `--font-size-base` 19px räckte inte piller-utrymmet längre för fyra alltid synliga namn -
  uppmätt överflöde ~10.66px (~5.3px på var sida, `nav.nav-shell`s `BoundingBox` gick negativ).
  Löst med en egen, snävare storlek `:root[data-text-size="large"] .nav-shell ::deep
  .nav-link`/`.nav-label` (mindre `gap`/`padding`/`font-size`), scopad specifikt till Stor
  text-läget så den redan granskade normalstorleks-pillen inte rörs.
- **Nytt permanent test** `MobileNavTests.The_pill_still_fits_with_margin_in_large_text_mode`
  (ersätter det tillfälliga skärmbildstestet som hittade buggen) - mäter `nav.nav-shell`s
  `BoundingBox` i Stor text-läge och kräver ≥ 12px marginal på var sida. En riktig
  regressionsrisk (layoututrymmet är exakt beräknat, inte generöst tilltaget) motiverar att
  behålla testet permanent i stället för att bara verifiera en gång och kasta det, till
  skillnad från de flesta andra verifieringarna i detta uppdrag.
- Utökade `MobileNavTests.Scrolling_to_the_bottom_clears_the_floating_pill` med en assertion
  att "Rum"-länken är `ToBeVisibleAsync()` - inte bara finns i DOM:en - både före och efter
  scroll.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `MobileNavTests` 3/3
  (inklusive det nya permanenta testet). `Hemordna.E2E.Tests` 83/83 rent (82 tidigare + det
  nya permanenta testet) - kört i sin helhet, delad navigations-CSS. Skärmbilder (normal text
  och Stor text, 390px) granskade: alla fyra namn syns tydligt i båda lägena, pillen ryms med
  marginal i Stor text.

#### B1 (Ångra på Idag) — `IMPLEMENTED`

- **`MinDag.razor`**: `_undo` (`(Guid OccurrenceId, string Name)?`) sätts i `CompleteAsync`
  efter en lyckad `Api.CompleteOccurrenceAsync` (namnet slås upp i `_day.Items` INNAN
  `LoadDayAsync()` ersätter `_day` - occurrensen har inte hunnit flytta till `Completed` än vid
  det laget). Ett delat `UndoBar`-`RenderFragment` (`role="status"`, "Klar: {namn}" +
  "Ångra"-knapp) renderas direkt under `header.day-header` i listläge, och direkt under
  `.focus-card` i fokusläge - samma `_undo`-tillstånd, bara olika placering beroende på
  `IsFocusMode`, inte två samtidiga kopior.
- **8s-fönstret**: en `CancellationTokenSource` per "visa ångra"-anrop (`ShowUndo`) - en ny
  avbockning innan de första 8 sekunderna gått ut avbryter (`Cancel()`) den tidigare timern i
  stället för att låta två `Task.Delay`-anrop kapplöpa om att nollställa `_undo`.
  `Task.Delay(TimeSpan, CancellationToken)` (rent klient-UI, ingen domän-/Application-logik -
  CLAUDE.md §5:s `TimeProvider`-krav gäller inte en visuell auto-dismiss-timer på samma sätt
  som det gäller planeringslogik) fångar `TaskCanceledException` och returnerar tyst vid
  avbrott. `Dispose()` avbryter och kastar den kvarvarande `CancellationTokenSource`en.
- **"Ångra"** anropar `Api.ReopenOccurrenceAsync` (A1) och laddar om dagen vid lyckad ångring;
  ett avslag (utanför 15-minutersfönstret, fel person - borde i praktiken aldrig hända från
  denna knapp eftersom den bara syns för den som just bockade av) lämnar raden bockad utan
  felmeddelande, en medveten, minimal avvägning för ett fel som inte rimligen kan uppstå från
  UI:t självt.
- **`min-height: 44px` på `.undo-bar` självt** (inte en alltid närvarande, tom platshållare) -
  radens EGEN höjd är stabil oavsett hur texten/knappen laddar in, så listan under flyttas i
  ETT enda, förutsägbart steg när raden dyker upp, i stället för att reflowa flera gånger medan
  dess eget innehåll sätter sig. En medveten, enklare tolkning av "reservera utrymmet" än en
  permanent tom platshållare - dokumenterad här som ett aktivt val, inte en spec-avvikelse.
- **`task-swipe.js`**: `threshold` höjd 72 → 96px. Ny riktnings-låsning: de första 12px rörelse
  avgör om gesten är horisontell (fortsätt som svep) eller vertikal (`|dy| > |dx|` - avbryt
  helt, släpp pekar-capture, låt `touch-action: pan-y` sköta scrollningen resten av gesten;
  beslutet tas EN gång per gest, omprövas inte om fingret senare drar mer horisontellt).
- **Inte täckt av något E2E-test** (varken nytt eller sedan tidigare): själva
  svep-gestens JS-logik (tröskelvärde, riktningslåsning) - Playwright-simulerad pekar-drag för
  denna specifika interaktion har aldrig funnits i testsviten, och inget nytt sådant test
  efterfrågades i uppdraget. `NOT VERIFIED` för just gest-nivån; verifierat genom kodgranskning
  och att `[JSInvokable] OnSwipeCompleteAsync`/`OnSwipeDeferAsync`s kontrakt mot
  `TaskListItem.razor` är oförändrat.
- **Nytt permanent test** `UndoTests.Undo_brings_a_completed_task_back_and_the_offer_expires_on_its_own`
  - bockar av, ångrar, bekräftar raden är tillbaka som vanlig utestående uppgift; bockar av
  igen och låter erbjudandet självdö (riktig 9s väntan, inte en simulerad klocka - matchar
  uppdragets egen instruktion "vänta 9 s").
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `UndoTests` 1/1.
  `Hemordna.E2E.Tests` 84/84 rent (83 tidigare + `UndoTests`) - kört i sin helhet. Skärmbilder
  (ljust/mörkt, 390px) granskade: ångra-raden syns tydligt mellan rubrik och lista, ingen
  överlappning, "Klart idag" visar den avbockade uppgiften korrekt genomstruken.

#### B2 (Stabil lista vid realtidsändring) — `IMPLEMENTED`

- **Problemet**: `OnOccurrencesChanged` (realtidshändelsen från `HouseholdRealtimeClient`, en
  annan medlems egen handling) anropade tidigare `LoadDayAsync()` direkt - en fullständig
  omritning som kunde slänga listan mitt i ett svep, en expanderad rad, eller ett öppet ark, och
  som alltid ritade om ordningen från grunden. Idag är den enda sidan i appen där medlemmen
  aktivt trycker/sveper/expanderar rader - se Del C: hushållet är normalt blandat, och detta kan
  komma från vilken annan medlems handling som helst, när som helst.
- **`MinDag.razor`**: `OnOccurrencesChanged` anropar nu `HandleRemoteChangeAsync`, som gör ett av
  två saker:
  1. **Upptagen** (`IsInteractionBusy()`: `_showUnplannedSheet`/`_showExtraTaskSheet` öppet,
     eller `_lastInteraction` yngre än 2s) - startar om (`CancellationTokenSource`, samma mönster
     som `ShowUndo`) en 2s-timer som prövar igen; en andra ändring som kommer in medan den väntar
     ersätter timern i stället för att kapplöpa med den.
  2. **Ledig** - anropar `ReconcileRemoteChangeAsync()` direkt.
  `_lastInteraction` sätts i `CompleteAsync`, `DeferAsync`, `ToggleExpand`, `OpenUnplannedSheet`
  (ny metod - ersätter den tidigare inline-lambdan `() => _showUnplannedSheet = true`, som annars
  inte kunde sätta `_lastInteraction`) och `OpenExtraTaskSheetAsync`.
- **`ReconcileRemoteChangeAsync()`**: hämtar en färsk plan till en temp-variabel och patchar
  `_day` "på plats" i stället för att ersätta den:
  - En rad som fortfarande är utestående i den nya planen behåller sin plats oförändrad.
  - En rad som inte längre är utestående OCH nu finns i `incoming.Completed` läggs till i
    `_remotelyCompletedIds` och **stannar kvar** i `_day.Items` (ritas som
    `RemotelyCompletedRow` - dämpad, ifylld bock, "Klar: {namn}" - i stället för att flyttas till
    "Klart idag" eller försvinna). En rad som försvunnit av annan anledning (uppskjuten,
    borttagen) faller bort tyst, precis som en vanlig omladdning redan skulle göra.
  - En genuint ny occurrence (schemalagd av någon annan, eller Idags egen
    occurrence-generering som hunnit ikapp) läggs sist i sin rumsgrupp - `RoomGroups`/
    `FloorGroups` grupperar redan på `AreaName` och bevarar första-förekomst-ordning, så att
    lägga till sist räcker; själva grupperingen ändras inte.
  - `Completed` sätts till `incoming.Completed` MED alla `_remotelyCompletedIds` filtrerade
    bort - annars skulle samma uppgift räknas och ritas två gånger (en gång som kvarliggande rad
    i "Övrigt"/rumsgruppen, en gång i "Klart idag"). Hittades och fixades via det tillfälliga
    E2E-testet nedan: "1 av 1 klara" visade felaktigt "2 av 2 klara" innan fixen.
  - `_day` byts aldrig ut i sin helhet av en realtidshändelse - bara av medlemmens egen nästa
    handling (`CompleteAsync`/`DeferAsync`/... anropar redan alla `LoadDayAsync()`) eller genom
    att lämna och öppna sidan igen. `LoadDayAsync()` nollställer `_remotelyCompletedIds`,
    `_remoteNote` och avbryter en väntande `_remoteRetryCts` - en full omladdning ersätter helt
    det patchade tillståndet den byggdes ovanpå.
- **`remote-note`**: `<p class="remote-note" role="status">` direkt under `.day-header`, synlig
  i både list- och fokusläge (samma placering oavsett `IsFocusMode`, till skillnad från
  `UndoBar` som har två renderingsplatser). Ren information, aldrig en jämförelse mellan
  medlemmar (Del C) - och aldrig ett riktigt namn: varken `PlannedTaskResponse` eller
  `DailyPlanResponse` bär vem som bockade av en occurrence (bekräftad kontraktslucka), så texten
  blir alltid "Någon annan bockade av {uppgift}." för en uppgift, "{N} uppgifter blev klara av
  andra." för flera. Försvinner efter 6s, samma `CancellationTokenSource`-mönster som
  `UndoBar`s 8s.
- **`RemotelyCompletedRow`**: "Klar: {uppgiftens eget namn}" - inte ett personnamn (finns inte i
  kontraktet), av samma anledning och med samma fras-konvention som `UndoBar`s "Klar: {namn}".
  Ingen `TaskListItem` - raden är inte längre interaktiv (inget svep, ingen expansion, inget kvar
  att skjuta upp), samma dämpade/genomstrukna behandling som `.task-list-done .task` redan har.
- **`OutstandingCount`**: en kvarliggande men avbockad rad får inte längre räknas mot "N kvar" i
  gruppens rubrik - `GroupHeading`s räkneargument byttes från `.Count` till
  `OutstandingCount(...)` som filtrerar bort `IsRemotelyCompleted`-rader.
- **`Hushall.razor`**: oförändrad - har redan sin egen, direkta omladdning vid samma
  realtidshändelse (bekräftat via kodgranskning), och uppdraget är uttryckligt att den ska
  behålla den.
- **Inget nytt API-kontrakt**: `PlannedTaskResponse`/`CompletedTaskResponse`/`DailyPlanResponse`
  rörs inte - hela lösningen är ett rent klientlager (`TaskRow`-posten lägger bara
  `IsRemotelyCompleted` ovanpå den befintliga `PlannedTaskResponse` för rendering).
- **Tillfälligt E2E-test** (skrivet, kört, sedan raderat - samma konvention som tidigare
  `DEBUG_*`-tester denna session): bockade av en occurrence direkt via HTTP medan sidan var öppen
  i webbläsaren (simulerar "någon annan"), bekräftade att `remote-note`n visas med rätt text,
  att raden stannar kvar som `.task-done-remote` med "Klar: Diska", att bocka-knappen försvinner,
  att "1 av 1 klara" stämmer (inte "2 av 2" - se dubbelräkningsbuggen ovan) och att notisen
  försvinner av sig själv inom 7s. Inget namngivet permanent E2E-test för B2 efterfrågades i
  uppdragets egen lista ("Nya E2E-tester"), så ingen permanent testfil lades till för just detta.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring, ingen påverkan väntad eller
  sedd). Tillfälligt E2E-test grönt (se ovan), sedan raderat. `Hemordna.E2E.Tests` i sin helhet
  kört (ren klientändring - se den skalade ner testpolicyn i konversationen).

**Uppföljning (samma dag, efter merge till `main`)**: kontraktsluckan ovan täpptes till.
`CompletedTaskResponse` (Api och Client) fick `Guid? CompletedByMemberId`, populerad direkt
från den redan befintliga `TaskOccurrence.CompletedByMemberId` i `HouseholdEndpoints.ToResponse`,
utan något nytt i domän eller Application eftersom `PlanCandidate` redan bar hela `Occurrence`.
`remote-note` slår nu upp namnet mot `_household.Members` (nu laddat direkt i
`OnInitializedAsync`, inte bara vid "Extra uppgift"): "Helena bockade av Diska." när medlemmen
hittas, "Någon annan" annars (lämnat hushållet, eller null - ett försvarsfall, inte förväntat i
praktiken).
`RemotelyCompletedRow`s egen text ("Klar: {uppgiftens namn}") rördes INTE - att byta den mot ett
personnamn hade gjort raden tvetydig (vilken uppgift?), personens namn hör hemma i den redan
fullständiga meningen i `remote-note`, inte i den terserade raden. Nytt permanent test
`RemoteCompletionNameTests.A_real_household_members_name_appears_instead_of_someone_else` (två
riktiga konton, samma inbjudningskodsmönster som `MixedHouseholdTests`) - grönt.

#### B3 ("Lugn" ska göra något) — `IMPLEMENTED`

- **Problemet**: `MotivationLevel.Calm` har funnits i `MemberPreference` sedan tidigare
  (`Domain/Households/MemberPreference.cs`, `Installningar.razor`s `motivation`-radiogrupp) och
  gick att välja och spara - men ingenstans i klienten lästes eller visades något baserat på
  värdet. Ett val som inte gör något är precis den sortens mekanism uppdraget åtgärdar: valet
  fanns, effekten fanns inte.
- **`MinDag.razor`**: `_motivation` (nytt fält, läses från `GetPreferenceAsync` bredvid
  `_presentation` i `OnInitializedAsync`). När `_motivation == "Calm"` renderas
  `<p class="day-encouragement">` direkt under `p.day-counts`, innanför samma
  `@if (hasDayCounts)`-block (så den aldrig visas för en tom dag) och i `header.day-header`
  (samma header som används i både list- och fokusläge - ingen separat kopia för
  `IsFocusMode`).
- **`EncouragementFor(outstanding, completed, total)`**: rent deterministisk, prövad i exakt
  denna ordning (spegel av uppdragstexten):
  1. `outstanding == 0 && completed > 0` → "Det viktigaste är gjort."
  2. `completed * 2 >= total && outstanding > 0` → "Det viktigaste är gjort."
  3. `outstanding > 4` → "En sak i taget räcker."
  4. annars → "Här är dina uppgifter för idag."
  Samma tillstånd (samma `Items.Count`/`Completed.Count`/totalt) ger alltid samma fras - ingen
  slumpmässig variation, ingen tidsbaserad rotation. Frasernas ordning i `EncouragementPhrases`
  (en `static readonly string[]`) följer samma ordning som villkoren, med en kommentar som
  pekar på DESIGN.md §5:s "Tillåtet"-lista - alla tre fraser klarar den listan (inga idiom,
  inga jämförelser mellan medlemmar, ingen skuldbeläggning).
- **`.day-encouragement`** (CSS): en tyst andra rad, samma tonvikt som `.day-counts` (`--sot-
  soft`, mindre textstorlek) - ingen egen bakgrund eller ram, ingen banderoll. Ingen ny
  animation, ingen `prefers-reduced-motion`-hänsyn behövs (statisk text, ingen in/ut-övergång).
- **`"None"`**: renderar ingenting, exakt som specen kräver - `_motivation` är antingen
  `"Calm"` eller `"None"` (aldrig `null` i praktiken efter `Installningar.razor`s egen
  `?? "None"`-fallback, men `_motivation == "Calm"` är ändå det enda villkoret som slår på
  - `null`/`"None"`/vad som helst annat visar inget).
- **Del C**: `_motivation` är redan en `MemberPreference` (per medlem sedan tidigare, inte nytt
  i B3) - ingen ändring krävs för att hålla det utanför delade ytor (Hushåll/Vecka/Rum visar
  aldrig `.day-encouragement`, den finns bara på `MinDag.razor`).
- **Nytt permanent test** `CalmMotivationTests`:
  - `Calm_with_half_the_days_tasks_done_shows_the_most_important_is_done_phrase` - Lugn +
    1 av 2 klara (regel 2, inte regel 1: `outstanding = 1 > 0`) → "Det viktigaste är gjort."
    (exakt uppdragets eget exempel).
  - `None_shows_no_phrase_at_all` - motivation lämnad odiskuterad (default `"None"`), 0 av 1
    klar → `.day-encouragement` finns inte i DOM:et.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `CalmMotivationTests` 2/2.
  `grep -rniE "NPF|ADHD|autis|funktionsned|tillgänglig"` mot ändrade filer: noll träffar.
  `Hemordna.E2E.Tests` i sin helhet kört (ren klientändring).

**Uppföljning: fem fraser i stället för tre, ett läge var.** De ursprungliga tre fraserna
delade en fras ("Det viktigaste är gjort.") mellan två olika tillstånd (allt klart, och hälften
eller mer klart) - `EncouragementFor` skiljer nu de fem tillstånden åt, en fras var, i denna
ordning: `outstanding == 0 && completed > 0` → `null` (se nedan) · `completed * 2 >= total &&
outstanding > 0` → "Det viktigaste är gjort." · `completed > 0` → "Vill du fortsätta där du
slutade?" · `outstanding > 4` → "En sak i taget räcker." · annars → "Här är dina uppgifter för
idag." `EncouragementFor` returnerar `string?` numera, inte `string` - `null` exakt när allt är
klart, eftersom `.calm-state` redan visar samma sak ("Dagens uppgifter är klara.") som sin egen
rubrik då; att också rendera `.day-encouragement` hade sagt det två gånger på samma skärm.
Anropsstället (`MinDag.razor`) beräknar `encouragement` en gång i samma kodblock som
`hasDayCounts`/`completedCount`, och villkorar `<p class="day-encouragement">` på
`encouragement is not null` i stället för på `_motivation == "Calm"` direkt - samma
"beräkna en gång, rendera på resultatet"-mönster som `hasDayCounts` redan följde.
`CalmMotivationTests` utökad till sex fall: ett per fras (inklusive den `null`-returnerande
"allt klart"-grenen, verifierad genom att `.day-encouragement` inte syns ALLS OCH att
"Dagens uppgifter är klara." bara finns en gång på sidan) plus `None`-fallet.

#### B4 (Fokusläget: "Visa nästa") — `IMPLEMENTED`

- **Problemet**: i fokusläge (`OneAtATime`) visade `.focus-card` alltid den första utestående
  uppgiften i ordningen - ingen väg förbi den utan att bocka av eller skjuta upp den, även om
  medlemmen bara ville se vad som väntade längre fram.
- **`MinDag.razor`**: `FocusTask` (tidigare en beräknad `.FirstOrDefault()`) delades i två:
  `FocusOrder` (samma `OverdueItems.Concat(RoomGroups...)`-kedja som förut, nu materialiserad
  till en `List<PlannedTaskResponse>` i stället för att bara ta första träffen) och `FocusTask`
  som indexerar `FocusOrder[_focusOffset % FocusOrder.Count]` (`null` om listan är tom - samma
  `@CalmState`-fallback som innan). Rader en `ReconcileRemoteChangeAsync` markerat
  `IsRemotelyCompleted` filtreras fortfarande bort - de är inte längre någons "nästa".
- **`_focusOffset`** (nytt `int`-fält, default 0): ökar med ett vid varje tryck på "Visa nästa"
  (`ShowNextFocusTask`) - ingen egen modulo-räkning vid ökningen, `FocusTask`s egen `%
  FocusOrder.Count` håller den inom gränserna oavsett hur många gånger den ökats. Nollställs i
  `LoadDayAsync()` - en ny dag, en omladdning efter egen handling, eller att lämna och komma
  tillbaka till sidan börjar alltid om från den första uppgiften i ordningen, aldrig kvar på en
  tidigare "nästa"-position.
- **Knappen**: tredje knappen i `.focus-actions`, `class="btn btn-link"` (skiljer den visuellt
  från de två primära handlingarna "Bocka av"/"Skjut upp till imorgon" - det här är en titt,
  inte en handling), dold när `FocusOrder.Count <= 1` (inget att rotera till).
- **Ändrar ingenting på servern eller på Vecka**: `ShowNextFocusTask` rör varken `_day`, någon
  `Api.*`-anrop eller occurrensernas ordning/status - rent lokalt UI-tillstånd, samma kategori
  som `_expandedOccurrence`.
- **Nytt permanent test** `FocusNextTests.Visa_nasta_cycles_the_focus_card_without_changing_the_days_schedule`
  - tre uppgifter, "Visa nästa" tryckt tre gånger visar tre olika namn och går sedan runt till
  det första igen (bevisar cykeln, inte bara "byter till NÅGOT"); en avslutande `GET .../plan`
  bekräftar att alla tre fortfarande är i `items` (ingen flyttad till `completed`) - "ändrar
  inget på servern" verifierat, inte bara antaget.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `FocusNextTests` 1/1.
  `grep -rniE "NPF|ADHD|autis|funktionsned|tillgänglig"` mot ändrade filer: noll träffar.
  `Hemordna.E2E.Tests` i sin helhet kört (ren klientändring).

#### B5 (Tid som nivåord) — `IMPLEMENTED`

- **Problemet**: `MemberPreference.ShowTimeLevel` (A2) fanns i kontraktet och gick att spara i
  Inställningar, men styrde ingenting i klienten - samma "val utan effekt"-mönster som B3:s
  `Calm`. Dessutom visade `Rum.razor` alltid minuter som råa siffror (`Totalt: N uppgifter · M
  min`, veckokapacitet) - PRODUCT.md §4/§8:s "tid är en planeringsingång, inte något att räkna
  i minuter" gällde bara delar av appen.
- **`TimeLevel.LabelFor(int minutes)`** (`Support/TimeLevel.cs`): närmaste nivåns etikett bland
  ENDAST de tre positiva nivåerna ("Lite tid"/"Lagom tid"/"Lång tid") - `MinBy` på `All.Where
  (level.Minutes > 0)`, aldrig "Ingen tid". "0 minuter → ingen chip alls" är uppringarens eget
  villkor (`EstimatedMinutes > 0`), inte något `LabelFor` självt uttrycker.
- **`TaskListItem.razor`**: ny parameter `ShowTimeLevel` (`bool`, default false). När sann och
  `Item.EstimatedMinutes > 0`: `<span class="chip chip-time">@TimeLevel.LabelFor(...)</span>`
  direkt efter namnet/rumschipen. `.chip-time` (ny CSS): en konturchip (`border: 1px solid
  var(--edge)`, transparent bakgrund, `--sot-soft`) snarare än en fylld - så den läses som ett
  lugnare, sekundärt faktum bredvid rummets egen fyllda `.chip`, aldrig konkurrerar med den.
- **`MinDag.razor`**: `_showTimeLevel` (nytt fält, läst från `GetPreferenceAsync` bredvid
  `_motivation`) skickas som `ShowTimeLevel="_showTimeLevel"` till båda `<TaskListItem>`-
  användningarna ("Sedan tidigare" och rumsgrupperna). Fokuskortet visar samma chip direkt
  under `h2.focus-name`, samma `_showTimeLevel && focusTask.EstimatedMinutes > 0`-villkor -
  ingen dubblettlogik, bara samma mönster på två ställen eftersom fokuskortet inte går genom
  `TaskListItem`.
- **`Rum.razor`**: `Totalt: N uppgifter · M min`, den frekvensvägda `Ungefär … min/vecka`-
  raden och hushållets kapacitetsnotis flyttades in i `<details class="more-options">
  <summary>Visa tid</summary>` - samma disclosure-mönster som redan fanns för "Lägg till ett
  tomt rum i stället". Siffrorna själva är oförändrade (bara Idag ska ALDRIG visa minuter som
  siffra - Rum får fortsätta göra det, bakom en frivillig disclosure snarare än alltid synligt).
  `TaskWorkloadTests`/`OmradenTests`: uppdaterade till att klicka `Visa tid` innan de letar
  efter texten - vad de kontrollerar är oförändrat.
- **Nytt permanent test** `TimeLevelTests.Toggled_on_shows_a_time_level_chip_and_never_the_minute_count`
  - av som standard: ingen chip, ingen "5 min" någonstans. Påslaget: `.chip-time` visar "Lite
  tid" för en 5-minutersuppgift, och varken "5 min" eller den fristående siffran "5" finns i
  DOM:et i något av lägena.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `TimeLevelTests` 1/1,
  `TaskWorkloadTests` + `OmradenTests` (uppdaterade) 16/16 grönt tillsammans.
  `grep -rniE "NPF|ADHD|autis|funktionsned|tillgänglig"` mot ändrade filer: noll träffar.
  `Hemordna.E2E.Tests` i sin helhet kört (klientändring).

#### B6 (Tak på "Sedan tidigare") — `IMPLEMENTED` — avvikelsen nedan senare löst, se uppföljningen

- **Problemet**: "Sedan tidigare" visade alla försenade uppgifter oavsett antal - en dag med
  många förseningar blev en lång, tät lista som lästes som ett misslyckande snarare än en plan.
- **`AVVIKELSE FRÅN SPECEN` - `OriginalScheduledDate` finns inte i kontraktet.** Specen bad om
  "de 3 äldsta (lägst `OriginalScheduledDate`, sedan namn)". `PlannedTaskResponse` (Api OCH
  Client, `Contracts/ApiContracts.cs`/`Contracts/HouseholdContracts.cs`) bär `OccurrenceId`,
  `TaskDefinitionId`, `Name`, `EstimatedMinutes`, `Priority`, `IsOverdue`, `AreaName`,
  `Description`, `CanBeDeferred` - inget datum. `TaskOccurrenceResponse` (en annan DTO, från
  `/occurrences`-endpointen) har visserligen `OriginalScheduledDate`, men det är inte samma typ
  som `MinDag.razor` faktiskt läser. Att lägga till fältet hade krävt att röra Api-kontraktet
  utanför Del A:s "två additiva ändringar" - samma sorts eget-initiativ-tillägg uppdraget
  uttryckligen varnar för (jf. B2:s "vem bockade av"-exempel). I stället: de tre första i den
  ordning `OverdueItems` REDAN har (samma deterministiska, serverstyrda ordning "Sedan
  tidigare" alltid visat, oförändrad av B6) - inte en omsortering efter datum. Konsekvens,
  synlig i skärmbildsgranskningen: de tre synliga raderna är INTE nödvändigtvis de tre
  kronologiskt äldsta. **Rapporteras här enligt uppdragets egen instruktion snarare än att
  API-fältet läggs till på eget initiativ - stanna och fråga om `OriginalScheduledDate` ska
  exponeras.**
- **`MinDag.razor`**: `OverdueCapThreshold = 5`, `OverdueVisibleCount = 3` (namngivna
  konstanter, inte magiska tal). När `OverdueItems.Count > 5` och `!_showAllOverdue`: bara de
  tre första renderas, följt av `<li class="task task-more">` ("… och N-3 till", "Visa alla" →
  `_showAllOverdue = true`, "Låt Hemordna sprida ut dem" → `RebalanceOverdueAsync`).
  `GroupHeading`s räknare (`OutstandingCount(OverdueItems)`) räknar fortfarande hela listan,
  capad eller inte - rubriken ljuger aldrig om hur mycket som väntar. `_showAllOverdue`
  nollställs INTE i `LoadDayAsync` (till skillnad från `_focusOffset`) - ett medvetet val: att
  slå av "Visa alla" igen varje gång medlemmen bockar av en annan uppgift hade känts som att
  valet inte höll i sig.
- **`RebalanceOverdueAsync`**: `Api.RebalanceScheduleAsync(householdId)` → `LoadDayAsync()` →
  DÄREFTER `_rebalanceStatus = "{N} uppgifter fördelades på andra dagar."` (ordningen spelar
  roll: `LoadDayAsync` rör inte `_rebalanceStatus`, så att sätta strängen EFTER omladdningen är
  vad som gör att den syns kvar även om "Sedan tidigare" krympt eller försvunnit helt).
  Statusraden (`<p class="notice" role="status">`) har ingen egen timeout - specen angav ingen
  (till skillnad från B1/B2/B7 som alla har explicita sekundtal), tolkat som att den ska stå
  kvar tills sidan lämnas, inte tystas efter ett gissat antal sekunder.
- **`.task-more`** (CSS): `flex-wrap` + `row-gap` så raden med räknare + två länkknappar bryter
  snyggt på smala skärmar i stället för att tvinga fram horisontell scroll. Ingen egen
  `min-height`-justering på knapparna - de ärver `.btn-link`s 44px (DESIGN.md §10 är
  ovillkorlig; ett första utkast som satte `min-height: auto` på dem togs bort igen innan
  commit).
- **Nytt permanent test** `OverdueCapTests.Seven_overdue_tasks_show_three_plus_a_count_until_visa_alla`
  - sju försenade uppgifter → exakt 3 avbockningsbara rader + "... och 4 till"; rubriken visar
  fortfarande "7 kvar"; `Visa alla` avslöjar alla sju och `.task-more`-raden försvinner.
  "Låt Hemordna sprida ut dem" har ingen egen namngiven test i specens lista - verifierat med
  ett tillfälligt E2E-test (skrivet, kört, raderat): knapptrycket visar statusraden
  "0 uppgifter fördelades på andra dagar." (inga återkommande uppgifter fanns att flytta i det
  testfallet - se skärmbild, granskad, ingen layoutbugg).
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `OverdueCapTests` 1/1.
  Tillfälligt rebalance-test grönt, sedan raderat. `grep -rniE
  "NPF|ADHD|autis|funktionsned|tillgänglig"` mot ändrade filer: noll träffar.
  `Hemordna.E2E.Tests` i sin helhet kört (klientändring).

**Uppföljning (samma dag, efter merge till `main`)**: kontraktsluckan ovan täpptes till.
`PlannedTaskResponse` (Api och Client) fick `DateOnly OriginalScheduledDate`, populerad direkt
från `TaskOccurrence.OriginalScheduledDate` i `HouseholdEndpoints.ToResponse` (samma mönster som
`CompletedByMemberId` ovan - ingen domän-/Application-ändring). `OverdueItems` sorterar nu
`.OrderBy(OriginalScheduledDate).ThenBy(Name)` - de tre synliga vid tak är på riktigt de tre
kronologiskt äldsta, inte bara de tre servern råkade lista först. Nytt permanent test
`OverdueCapTests.Capped_overdue_items_are_the_oldest_by_original_scheduled_date_not_server_order`,
sju uppgifter schemalagda i blandad ordning där en har ett namn som sorterar sist alfabetiskt
men är schemalagd längst tillbaka i tiden - bevisar att datumet, inte namnet eller
skapelseordningen, är den faktiska sorteringsnyckeln.

#### B7 (Knappar som alltid finns) — `IMPLEMENTED`

- **Problemet**: "Flytta till en annan dag" rendrades bara när `_day.Unplanned.Count > 0` -
  chip-raden bytte alltså form beroende på ett tillstånd som inte syns förrän man redan tittar
  på den. En knapp som ibland finns och ibland inte är precis den sortens oförutsägbarhet
  uppdraget åtgärdar (jf. B9:s "namnen alltid synliga" - samma princip, en annan yta).
- **`MinDag.razor`**: chippet rendras nu ALLTID, i samma ordning. När
  `_day.Unplanned.Count == 0`: klassen `chip-action-disabled` (opacitet, ingen
  bakgrundsändring - `.chip-action-disabled` är bara en av flera samtidiga signaler, aldrig
  ensam bärare av "avstängd") och `aria-disabled="true"`.
- **`aria-disabled`, inte `disabled`** - ett medvetet val, inte en genväg: `disabled` hade tagit
  bort knappen ur tabb-ordningen helt, vilket motverkar precis den förutsägbarhet chippet finns
  till för (samma knapp på samma plats, oavsett dagens tillstånd - även för tangentbords-/
  switch-navigering). `OpenUnplannedSheet` grenar därför på `_day.Unplanned.Count`: noll →
  `ShowUnplannedNotice()` (statusrad "Inget att flytta just nu.", 4s, samma
  `CancellationTokenSource`-mönster som `ShowUndo`/`ShowRemoteNote`); annars → öppnar arket som
  förut. **Playwright-fångst**: `ClickAsync()` vägrar av sig själv klicka ett
  `aria-disabled="true"`-element (dess egen "actionability"-heuristik tolkar det som `disabled`,
  trots att en riktig muspekare inte bryr sig om `aria-disabled`) - testet nedan använder
  `ClickAsync(new() { Force = true })` för att testa det verkliga, tillåtna beteendet i stället
  för Playwrights konservativa gissning.
- **Inga befintliga tester påverkades**: varken `ExtraTaskTests.cs` eller `TaskIconsTests.cs`
  (den enda befintliga referensen till knappen) förlitar sig på att den saknas - ingen
  testuppdatering krävdes utöver det nya testet nedan.
- **Nytt permanent test** `AlwaysVisibleChipTests.With_nothing_unplanned_the_chip_stays_but_answers_with_a_status_line`
  - inget odisponerat: chippet syns, `aria-disabled="true"`, ett tvingat klick visar statusraden
  i stället för att öppna arket (bekräftat: dialogen öppnas INTE), och statusraden försvinner av
  sig själv. Inte namngivet i specens egen testlista - lades till ändå eftersom det är ny,
  tidigare otestad UI-logik.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `AlwaysVisibleChipTests` 1/1;
  `ExtraTaskTests`/`TaskIconsTests` 5/5 oförändrade och gröna.
  `grep -rniE "NPF|ADHD|autis|funktionsned|tillgänglig"` mot ändrade filer: noll träffar.
  `Hemordna.E2E.Tests` i sin helhet kört (klientändring).

#### B10 (Språkpass) — `IMPLEMENTED`

- **Problemet**: tre fraser på Vecka byggde på en bild/ett idiom i stället för att säga vad de
  gör - "Tjuvkika" (en gissningslek: vad innebär "tjuvkika" egentligen?), "Ser fördelningen
  skev ut?" (en bild av lutning, inte en fråga om vad knappen faktiskt gör), "Sprid ut över
  veckan" (sprider man verkligen ut något, eller flyttas uppgifter till andra dagar?).
- **`Vecka.razor`**: tre exakta textbyten enligt uppdraget - `<summary>Ser fördelningen skev
  ut?</summary>` → "Vill du fördela om dagarna?", knappens vilotext "Sprid ut över veckan" →
  "Fördela om dagarna", `<summary>Tjuvkika på ett schema</summary>` → "Se någon annans dag".
  Knappens BUSY-text ("Sprider ut..." → "Fördelar om...") följde med av samma anledning som den
  nya §5-regeln nedan finns - en knapps två tillstånd (vilande/upptagen) ska läsas som samma
  handling, inte två olika. `aria-label="Tjuvkikad dag"` → `"Den valda dagen"` (samma princip
  tillämpad på en skärmläsarsträng, inte bara synlig text - annars hade AT-användare fortfarande
  hört den gamla idiomatiska frasen även om sidan visuellt bytt språk).
  `_rebalanceMessage`-texterna ("En uppgift flyttades...", "Redan bra utspritt...") rördes INTE
  - redan sakliga, ingen idiom.
- **`docs/DESIGN.md` §5**: ny regel tillagd, ordagrant enligt uppdraget - "Inga idiom, inga
  metaforer, inga lekfulla omskrivningar. En knapp säger vad den gör." - med de tre bytena ovan
  som egna, konkreta exempel. §6 (Idag/Vecka): de återstående, nu inaktuella citaten av de gamla
  frascitaten uppdaterade till de nya - annars hade dokumentet självt brutit mot regeln det just
  fått. Samtidigt rättades ett redan inaktuellt påstående i Vecka-avsnittet om att Rum-totalen
  "behålls som dämpad text under brickorna" - stämde inte sedan B5 flyttade den bakom "Visa tid".
  §7/§8:s egna, större tillägg (snabbvalen, "Lugn" implementerad, namnen alltid synliga) hör till
  Del C:s samlade dokumentationspass i stället - samma rytm som redan hållits genom A1–B9 (bara
  ARCHITECTURE.md per commit; DESIGN.md/PRODUCT.md/HANDOFF.md i klump på slutet), med det här
  commitets två undantag (den nya §5-regeln, och de nu direkt felaktiga citaten) gjorda ändå
  eftersom att LÅTA dem stå fel hade varit värre än att vänta.
- **Testuppdateringar** (bara selektorer, aldrig vad testerna kontrollerar): `OmradenTests`
  (`Sprider_ut_veckan...`-scenariot), `PeekScheduleTests` (tre tester, samma
  `GetByText("Tjuvkika...")` → `GetByText("Se någon annans dag")`, plus `aria-label`-bytet).
  `PlaneringTests` hade inga träffar att uppdatera - ingen av dess assertions rörde dessa fraser.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `PeekScheduleTests`
  (3) + `OmradenTests` (10) + `PlaneringTests` (5) = 18/18 grönt. `grep -rniE
  "NPF|ADHD|autis|funktionsned|tillgänglig" src/Hemordna.Client --include="*.razor"`: noll
  träffar (DESIGN.md:s egna träffar på "tillgängligt namn"/"Tillgänglighet" är vanlig
  webbtillgänglighetsterminologi, utanför grepets mandat som gäller `.razor`). Repo-brett sök
  efter de gamla fraserna: bara historiska beslutsloggar i ARCHITECTURE.md (medvetet
  oförändrade - de beskriver vad som var sant DÅ) och DESIGN.md:s egna nya exempel-citat kvar.
  `Hemordna.E2E.Tests` i sin helhet kört (klientändring).

#### B11 (Lägesval i Inställningar) — `IMPLEMENTED`

- **Problemet**: A2/B3/B5/B8 byggde fyra oberoende, sparbara/enhetsval (presentation,
  motivation, `ShowTimeLevel`, `CalmScreen`) - men att faktiskt kombinera dem till "en lugnare,
  tydligare upplevelse" krävde att veta att alla fyra fanns och höra ihop. Inget i UI:t sa det.
- **`Installningar.razor`**: en rad chips (`<div class="chips" aria-label="Snabbval">`) överst i
  "Hur vill du se dina uppgifter?"-kortet, innan `.notice`-raden. Tre `Preset`-poster (en
  `private sealed record` med `Label`/`Presentation`/`Motivation`/`ShowTimeLevel`/`CalmScreen`):
  - **Kompakt**: `Text`, `None`, av, av.
  - **Tydlig**: `ImageAndText`, `None`, PÅ, av.
  - **Steg för steg**: `OneAtATime`, `Calm`, PÅ, PÅ.
  Inget namn på chippen säger vem den är för - bara vad den ställer in, i linje med uppdragets
  hårda krav (PRODUCT.md §7 får sin egen, uttryckliga version av samma regel i Del C:s
  dokumentationspass).
- **`ActivePreset`**: en beräknad egenskap (INTE ett en gång ihågkommet "senast tryckta chip"-
  tillstånd) som jämför de FYRA nuvarande fälten mot varje preset och returnerar den som
  matchar exakt, annars `null`. Ändrar medlemmen en enskild radioknapp eller växel för hand
  efteråt slocknar `chip-primary`-markeringen automatiskt - den ljuger aldrig om att en
  kombination fortfarande är ett namngivet läge när den inte längre är det.
- **Två olika "sparar"-betydelser i samma tryck**: `ApplyPresetAsync` sätter
  `_presentation`/`_motivation`/`_showTimeLevel` rent lokalt (osparat till servern förrän
  "Spara" trycks, exakt som att fylla i radioknapparna för hand) MEN anropar
  `CalmScreen.SetAsync` omedelbart - "Lugnare skärm" har (sedan B8) aldrig haft ett sparat/
  osparat tillstånd över huvud taget, den ÄR bara vad den är just nu, per enhet, precis som
  temat. Att låtsas den väntade på "Spara" hade varit en ny, påhittad regel; att den redan alltid
  varit omedelbar är den regel som redan gällde.
- **Ny lista** `<ul class="list" aria-label="Fler val">` (två `<li class="list-item">`,
  `<label class="field-check">` - INTE `field field-check`, spec bad uttryckligen om den
  fristående klassen eftersom `.list-item` redan ger radavstånd/kantlinje) mellan
  presentation-radioknapparna och `<h2>Motivation</h2>`: "Visa ungefär hur lång tid en uppgift
  tar" (`_showTimeLevel`, sparas med "Spara" som alla andra fält i kortet) och "Lugnare skärm –
  inga rörelser eller genomskinliga effekter" (`_calmScreen`, `ToggleCalmScreenAsync`, samma
  omedelbara `CalmScreen.SetAsync`-anrop som en chip gör). "Gäller den här enheten" (`muted
  small`) under den senare - den exakta strängen specen angav; ingen tidigare identisk fras
  fanns att återanvända (temats egen är en längre mening, "Det här gäller bara den här
  enheten, inte hushållet eller dina andra enheter.").
- **`<h2>Motivation</h2>` orört** utöver att den nu faktiskt gör något (B3) - ingen ny text,
  ingen ny logik här.
- **Nya/uppdaterade tester**:
  - `InstallningarTests.Steg_for_steg_sets_all_four_choices_and_kompakt_resets_them` - "Steg för
    steg" sätter alla fyra (inklusive att chippet själv visas `chip-primary`), "Kompakt"
    nollställer alla fyra.
  - `ThemeTests.Toggling_calm_screen_sets_and_clears_data_calm_immediately_without_saving` -
    `data-calm` sätts/tas bort direkt vid växling, ingen "Spara" inblandad (samma fil som redan
    äger `data-theme`-motsvarigheten, för samma "per enhet, omedelbart"-familj av beteende).
  - `SkarmbilderTests`: `07b-installningar-steg-for-steg` tillagd direkt efter `07-installningar`
    - trycker "Steg för steg", tar bilden, trycker sedan OMEDELBART "Kompakt" igen för att
      återställa `data-calm` (ett upptäckt, nödvändigt steg: `CalmScreen`s omedelbara,
      `localStorage`-baserade tillstånd hade annars läckt in i alla efterföljande skärmbilder i
      samma testkörning - `08-idag-bild-text` och framåt - eftersom `SetPresentationModeAsync`
      bara sköter presentation-radioknappen, aldrig `motivation`/`calmScreen`).
  - **Ett kortvarigt, felaktigt intryck under granskning**: `chip-primary`/`chip-action` ser
    mycket lika ut vid en snabb blick på skärmbilden (`--gustav-soft` mot `--surface-2`, båda
    ljusa, olika nyans snarare än ljushet) - verifierat med ett tillfälligt debug-test (`class`-
    attributet läst direkt, `chip-action chip-primary` bekräftat närvarande) i stället för att
    lita på ögat, sedan raderat. Ingen kodändring behövdes - CSS:en fungerade redan korrekt.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring). `InstallningarTests` +
  `ThemeTests` 8/8. `SkarmbilderTests` 2/2, båda skärmbilderna granskade (ingen överlappning,
  kryssrutorna och "Gäller den här enheten" sitter rätt, inget kvarvarande `data-calm`-läckage
  i efterföljande bilder). `grep -rniE "NPF|ADHD|autis|funktionsned|tillgänglig"
  src/Hemordna.Client --include="*.razor"`: noll träffar. `Hemordna.E2E.Tests` i sin helhet
  kört (klientändring).

**Uppföljning: presentation/motivation/tid sparas direkt, ingen "Spara"-knapp längre.**
Kortet hade tidigare två olika regler samtidigt - tema och "Lugnare skärm" slog igenom direkt,
resten väntade på en knapp - vilket var oförutsägbart (samma sida, olika beteende beroende på
vilket fält). En regel gäller nu genomgående: allt i "Min visning" sparas i samma stund det
ändras. Varje `@onchange` (presentationsradio, motivationsradio, "Visa ungefär hur lång tid en
uppgift tar") och varje snabbvalschip anropar `SaveAsync` direkt i stället för att bara sätta
fält. `SaveAsync` är en `while`-loop innanför en `_saving`-vakt snarare än ett enda försök: ett
fält som ändras MEDAN ett sparande redan pågår startar aldrig ett andra, parallellt anrop - det
märks av loopen efter att det pågående anropet är klart och sparar då om, med de senaste
värdena. Misslyckas ett sparande återställs fälten till senast bekräftat sparade värden
(`_savedPresentation`/`_savedMotivation`/`_savedShowTimeLevel`) och en `.notice-problem` med en
"Försök igen"-knapp visas; lyckas det visas "Sparat" i två sekunder (samma
`CancellationTokenSource`-mönster som `MinDag.razor`s `remote-note`/`undo-bar`). "Byt
lösenord" är oförändrat - ett lösenordsbyte ska förbli en avsiktlig handling med sin egen
knapp, inte något som sparas medan man skriver.

#### Del C (Blandat hushåll) — `IMPLEMENTED`

- **Varför detta är en egen, avslutande del snarare än ett test bland de andra**: varje tidigare
  delmoment (A1–B11) byggde EN mekanism i taget; Del C är inte en ny mekanism utan ett bevis
  att de tolv redan byggda håller ihop när två medlemmar faktiskt har olika val samtidigt - det
  enda scenario resten av uppdraget aldrig testade explicit (varje tidigare test körde med en
  ensam medlem).
- **Invarianten (upprepad här som en uttalad regel, inte bara ett genomfört test)**: allt i
  Del A och B är antingen per medlem (`MemberPreference`: `Presentation`, `Motivation`,
  `ShowTimeLevel` - A2/B3/B5) eller per enhet (`hemordna.theme`, `hemordna.calm` - B8/B11).
  Ingenting läser eller skriver en annan medlems preferens; ingenting ligger på `Household`.
  Bekräftat genom kodgranskning (ingen ny `Household`-egenskap i hela uppdraget, se `git diff
  A1..HEAD -- src/Hemordna.Domain/Households/Household.cs` = tomt) och genom testet nedan.
- **Varför listan inte ritas om under en interaktion (B2) hör hemma i Del C:s princip**: en
  realtidshändelse kan komma från VILKEN ANNAN MEDLEM SOM HELST, när som helst - i ett blandat
  hushåll är detta inte en sällan förekommande edge case utan den normala driften. B2:s
  patch-på-plats-lösning skyddar alltså inte bara mot "min egen andra flik", utan mot precis
  den situation Del C handlar om.
- **Varför "Lugnare skärm" är per enhet men tid ("Visa tid") är per medlem**: skärmen är en
  egenskap hos apparaten någon råkar hålla i just då (en delad familjeplatta ska inte plötsligt
  bli följsam för alla för att en person satte på det på sin telefon) - tiden är en egenskap
  hos hur PERSONEN vill läsa uppgifter, oavsett vilken enhet hen råkar sitta med (se
  docs/PRODUCT.md §7, uppdaterad nedan).
- **15-minutersfönstret och "bara den som bockade av"** (A1) hör redan hemma här utan att vara
  Del C-specifikt: det är en tidsgräns och en identitetskontroll i domänen
  (`TaskOccurrence.Reopen`), inte en presentationsfråga - men värt att upprepa i sammanhanget:
  en kort, snäv ångerrätt (inte en oändlig redigeringshistorik) är vad som gör att en ångrad
  avbockning kan försvinna TYST ur "Senaste händelser" (Del C) utan att någon behöver undra om
  historiken manipulerats - fönstret är kort nog att det bara någonsin är den egna, nyss gjorda
  handlingen som kan tas tillbaka.
- **Nytt obligatoriskt test** `MixedHouseholdTests.A_mixed_household_never_leaks_one_members_choices_or_undone_actions_to_another`
  - två riktiga konton i samma hushåll via inbjudningskoden (samma mönster som
  `HouseholdInviteTests`), alla fem steg i uppdragets egen ordning:
  1. A väljer "Steg för steg" och sparar.
  2. B:s Idag: ingen `.chip-time`, ingen `.day-encouragement`, `.task-list` syns (inte
     `.focus-card`), inget `data-calm`; B:s Inställningar visar fortfarande "Text (standard)"
     och "Ingen".
  3. A bockar av och ångrar (direkt mot API:t - UI-sidan av ångra är redan `UndoTests`s jobb,
     det här testar vad en ANNAN medlem ser efteråt). B:s Idag och Hushåll innehåller varken
     uppgiftens namn eller ordet "ångra" i sin helhet (`.app-main`s hela textinnehåll
     genomsökt, inte bara en enskild rad).
  4. A:s och B:s Hushåll-sidor: `.app-main`s hela textinnehåll jämfört tecken för tecken -
     identiskt. Sidan har ingen egen personalisering över huvud taget (ingen "Du"-etikett,
     ingen hälsning) så detta är i praktiken samma kontroll som "inget nytt textinnehåll läcker
     in", inte bara "ser ungefär likadan ut".
  5. B sätter på "Lugnare skärm"; A:s separata browser-context saknar `data-calm`.
  - **En genuin fångst under testskrivandet, inte bara en bugg i testet**: steg 1:s
    "Steg för steg" sätter OCKSÅ `data-calm` på A:s EGET device omedelbart (samma
    omedelbara-per-enhet-beteende som B11 redan bygger på) - för att steg 5 ska testa vad det
    faktiskt påstår (läcker B:s växling till A, inte "har A redan satt på det själv av en
    annan, redan verifierad anledning") stänger testet uttryckligen av "Lugnare skärm" på A:s
    enhet igen direkt efter steg 1/2, innan steg 3–5 körs. Ingen produktionskod ändrades - det
    är korrekt att en preset omedelbart sätter skärmen, testet behövde bara en ren
    utgångspunkt för just den delen av kontrollen.
  - Kört tre gånger i rad isolerat för att utesluta flakighet i det multi-context/realtids-tunga
    flödet: grönt alla tre gångerna.
- **Verifierat**: `dotnet build Hemordna.slnx` (0 fel/varningar). `Hemordna.Domain.Tests`
  87/87, `Hemordna.Application.Tests` 177/177 (ren klientändring - Del C lade inte till någon
  ny domän-/Application-/Api-kod). `MixedHouseholdTests` 1/1 (×3 isolerade körningar).
  `grep -rniE "NPF|ADHD|autis|funktionsned|tillgänglig" src/Hemordna.Client --include="*.razor"`:
  noll träffar. `Hemordna.E2E.Tests` i sin helhet kört.

**Uppföljning (samma dag, efter merge till `main` och driftsättning), produktionsfeedback:**
"Se någon annans dag" (Vecka) visade uppgiftsnamn helt utan rumsangivelse - två likadant
namngivna uppgifter i olika rum ("Vädra rummet" i två sovrum) gick inte att skilja åt. Ett rent
visningsfel, inte en kontraktslucka: `PlannedTaskResponse`/`CompletedTaskResponse` bar redan
`AreaName`, `Vecka.razor` bara rendrade aldrig chippet. Fixat genom att lägga samma
`<span class="chip">@areaName</span>`-mönster `TaskListItem.razor` redan använder till båda
raderna i peek-listan, plus motsvarande `.task-name .chip`-styling i `Vecka.razor.css`. Nytt
permanent test
`PeekScheduleTests.Two_same_named_tasks_in_different_rooms_are_distinguishable_by_their_chip`.

---

### Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg — `IMPLEMENTED`

Fem löften styr denna revision, i samma anda som "Beslut: Ångra och stabil lista" ovan var styrd
av sina egna: (1) det någon annan lämnar kvar hamnar aldrig hos mig, (2) jag ser min morgondag på
Idag, (3) jag kan jobba i förväg och ta ledigt, (4) det jag gör i förväg märks och jag kan se hur,
(5) inget en person gör syns hos någon annan (samma Del C/G-invariant som redan gäller).

**Kvarlämnat stannar.** En förekomst vars `OriginalScheduledDate < today` är aldrig flyttbar via
"Balansera om vem som gör vad" (`RebalanceTaskAssignments`), oavsett hur skev kapacitetsfördelningen
är. Ägarbyte hos en redan försenad uppgift skulle bara flytta problemet, inte lösa det.

**Ledig dag är en hård uteslutning; tid i förväg är bara en förskjutning.** De två låter lika på
ytan ("håll tillbaka nya uppgifter från den här personen") men är arkitektoniskt olika saker:

- `MemberDayOff` filtreras bort i `RotationPicker.EligibleMembers` innan någon kvotberäkning görs
  - en medlem med ledig dag kan aldrig väljas, oavsett hur mycket kredit någon annan har.
- `MemberTimeCredit`s saldo (`MemberTimeCredit.BalanceOf`, golv 0, tak vid egen
  `WeeklyTimeBudget.TotalWeeklyMinutes`) läggs bara till en redan valbar medlems belastade minuter
  i `RotationPicker.PickNext` - det kan aldrig göra en utesluten medlem valbar, bara förskjuta
  ordningen mellan dem som redan är det.

**`BringForwardTo` rör bara `ScheduledDate`, aldrig `OriginalScheduledDate`.** Det är precis det
som gör att en förtaget uppgift aldrig blir "kvarlämnat" eller läser som försenad - `IsOverdueOn`
jämför uteslutande mot `OriginalScheduledDate`. Samma fält gör `IsBroughtForwardOn` (och därmed
`TaskListItem`s "I förväg"-chip) möjligt utan någon extra flagga.

**`MemberTimeCredit` är en ledger, aldrig ett muterbart saldofält** - samma
snapshot-inte-härlett-tillstånd-resonemang som `TaskAssignment` redan använder för
rotationskvoten. `Earned`/`Consumed` är två fabriker på samma tabell (`Minutes` positivt
respektive negativt); `BalanceOf` är en ren summa, golvad vid 0 (PRODUCT.md §8 förbjuder att någon
någonsin visas som "skyldig" tid) och takad vid medlemmens egen veckobudget (så en lång osparad
period inte läses som ett mål att jaga). Detta är medvetet BARA ett tal och en mening i UI - se
`GetMemberTimeCredit`/`MemberTimeCredit`s egna domänkommentarer - ingen historik, inget diagram,
ingen streak (CLAUDE.md §12).

**`DailyPlanner` gör ett enda undantag från sin egen budgetregel**: en uppgift som redan blivit
itagen i förväg (`TaskOccurrence.IsBroughtForwardOn`) hamnar alltid i `Items`, även när den trycker
dagen över budgeten - valet att göra den idag skedde redan när den togs i förväg, budgeten kan inte
ångra det i efterhand. `DailyPlan.RemainingMinutes` kan därför bli negativt; det är en ärlig bild
av att dagen tagit på sig mer än budgeten, inte ett fel att skydda mot.

**Del C/G-invariant, samma tabell som gäller sedan tidigare**: en ledig dag är neutral
planeringsinformation - synlig för hela hushållet på Veckas och Hushålls delade prickmatris
(`dot-off`, samma sätt `dot-done`/`dot-planned` redan är synliga). Tid i förväg är raka motsatsen -
`GetMemberTimeCredit`s endpoint har medvetet inget `memberId`-ruttparameter att fråga om någon
annans med; den svarar alltid bara den anropande medlemmens egen balans. Verifierat i
`MixedHouseholdTests.A_members_day_off_shows_to_the_rest_of_the_household_as_a_neutral_dot`
respektive `A_members_time_credit_never_leaks_to_another_member`.

**Två riktiga fel hittades och fixades under klientverifiering, inga i det ursprungliga scopet:**

1. `SetMemberDayOff`s `BringAllForward`-läge kastade en `DomainException` när `date` (dagen som
   markeras ledig) var densamma som `today` OCH minst en av medlemmens egna förekomster redan låg
   just den dagen - att "föra fram" något till den dag det redan ligger på är exakt vad
   `TaskOccurrence.BringForwardTo`s eget invariant-skydd stoppar. Upptäckt när "Ta ledigt idag"
   (F4) kopplades ihop med "Ta med till idag". Fixat genom att hoppa över (inte flytta) varje
   förekomst vars `ScheduledDate` redan är `today` i `BringAllForward`-loopen - se
   `SetMemberDayOffTests.BringAllForward_on_today_itself_is_a_safe_no_op_rather_than_throwing`.
2. Att hämta dagens och morgondagens plan SAMTIDIGT (`Task.WhenAll`) i `MinDag.razor` racade
   `EnsureOccurrencesGenerated`s egna "hämta-igen"-logik - båda anropen kunde läsa "ingen
   förekomst ännu" för idag samtidigt och skapade varsin, vilket dubblerade dagens uppgift.

**Uppföljning, riktigt produktionsfel hittat 2026-09-09.** Ett hushåll hade 12 förekomster som
visade "Sedan tidigare" trots att deras `ScheduledDate` redan låg på ett kommande datum -
`RebalanceSchedule.RescheduleBacklogAsync` (un-clustring av en gammal, klumpad backlog, se
"Beslut" ovan om varför den funktionen finns) återanvände `TaskOccurrence.DeferTo`, som med
flit BARA flyttar `ScheduledDate` och medvetet lämnar `OriginalScheduledDate` kvar (exakt rätt
för en medlem som själv väljer att skjuta upp en redan försenad uppgift - se
"Kvarlämnat stannar" ovan). För ett system-initierat omschema är det fel beteende: uppgiften
ska läsa som nyplanerad på sin nya dag, inte permanent "sedan tidigare". Fixat genom en ny,
egen domänmetod `TaskOccurrence.ReanchorTo(DateOnly)` som flyttar BÅDA datumen, och bytte
`RescheduleBacklogAsync` till den istället för `DeferTo` - `DeferTo` självt orört, dess egna
test (`Deferring_does_not_hide_that_an_occurrence_is_overdue`) gäller fortfarande. De 12
felaktiga raderna i produktion patchades direkt i databasen (samma effekt som `ReanchorTo`
skulle gett) eftersom bakgrundsjobbet redan hade körts; själva grundfelet fanns bara i koden,
inte i data i övrigt. Se `TaskOccurrenceTests.Reanchoring_moves_both_the_scheduled_and_original_date`
och `RebalanceScheduleTests.An_already_generated_outstanding_occurrence_due_today_stops_reading_as_overdue`.
   Fångat av ett befintligt, orelaterat E2E-test (`TaskFrequencyTests`), inte av ett nytt. Fixat
   genom att hämta dem i sekvens istället.

**Nya endpoints** (`Hemordna.Api.Endpoints.HouseholdEndpoints`): `PUT`/`DELETE`
`.../members/{memberId}/days-off/{date}`, `GET .../days-off`, `POST
.../occurrences/{id}/bring-forward`, `POST .../occurrences/{id}/undo-bring-forward`, `GET
.../time-credit` (self-only). `POST .../complete` tar nu en valfri kropp `{ today }` - klientens
egna lokala datum, inte bara serverns UTC-datum (se nästa stycke). `POST
.../tasks/{taskId}/occurrences` tar `addedAsExtra`.

**Uppföljning (samma dag): klient/server-"idag"-skillnaden löst för medlemsinitierade
endpoints.** Klienten löser "idag" via `TimeProvider.GetLocalNow()` (`MinDag.razor`/`Vecka.razor`),
medan servern löser sitt eget via `TimeProvider.GetUtcNow()` - i en tidszon med positiv UTC-offset
stämmer de två inte överens under fönstret mellan lokal midnatt och UTC-midnatt (t.ex. svensk
sommartid, UTC+2: cirka kl. 00.00–02.00 lokal tid). Upptäckt under denna revisions egen
E2E-körning (flera, till synes orelaterade, redan existerande tester föll samtidigt, alla med
samma "fel dag"-signatur). `POST .../occurrences/{id}/bring-forward` och `PUT
.../members/{memberId}/days-off/{date}` tar nu båda en valfri `{ today }`-kropp, `GET
.../time-credit` en valfri `today`-frågeparameter - samma mönster `POST .../complete` redan hade,
nu även faktiskt inkopplat vid klientens eget anropsställe (det var byggt men aldrig skickat).
`rebalance-assignments`/`activity/daily-summary` är medvetet oförändrade - hushållsomfattande
åtgärder där serverns egna, för alla medlemmar konsekventa "idag" redan är rätt val, inte en
brist att fixa.

---

### Beslut: Sju enkla lösningar — `IMPLEMENTED`

Sju små, oberoende klientmekanismer för ork, igångsättning och tydlighet - varje del sin egen
commit, ingen rör Domain/Application/Api. Samma bärande regel som Del C i "Beslut: Ångra och
stabil lista": allt är per medlem eller per enhet, aldrig ett hushållsgemensamt fält - se den
regeln där för det fullständiga resonemanget, inte upprepat sju gånger här.

| Del | Vad | Varför per medlem/enhet | Medvetet inte byggt |
|---|---|---|---|
| 7. Genvägar | `manifest.webmanifest` fick `shortcuts` till Idag och Rum | Statisk PWA-metadata, ingen egen data | Fler genvägar än de två efterfrågade |
| 4. Steg i beskrivningen | En rad per steg renderas som numrerad lista (`Support/TaskSteps.cs`) i den utfällda raden och fokuskortet | Beskrivningen är redan uppgiftens egen, delad text - ingen ny per-person-dimension | Redigering av en BEFINTLIG uppgifts beskrivning i `TaskOptionsSheet` - `TaskDefinition.ChangeDescription` finns i Domain (oanvänd, samma gap som `ChangeEstimatedMinutes` innan B5), men inget `PUT`-endpoint exponerar den; att lägga till ett är backend-arbete utanför detta uppdrags scope. Beskrivningsfältet med platshållaren byggdes bara där det redan gick utan ny endpoint: "Extra uppgift" (skapar en ny uppgift, `CreateTaskRequest.Description` fanns redan). Se rapporten för denna del. |
| 3. Börja här | Planerarens första uppgift (samma ordning som `FocusOrder`) får en `chip-today`-chip i listläget | Härledd rent klientsidan från redan hämtad, hushållsgemensamt planerad data - inget nytt tillstånd att lagra alls, varken per medlem eller per enhet | Ingen synlighet för andra medlemmar (varje medlems egen `_day` avgör sin egen "första") - redan garanterat av att `_day` aldrig delas mellan medlemmar |
| 2. Jag börjar nu | En uppgift markeras "Pågår", flyttas överst i sin grupp (visning), döljer "Börja här" (`Support/StartedTask.cs`) | Per enhet OCH per dag (`hemordna.started`, samma mönster som `CalmScreen`/`Theme`) - vem som "håller på med" något just nu är en egenskap hos enheten i handen, inte hushållets data | Timer, tidmätning, historik över påbörjade uppgifter - bara EN markering, ingen tid räknas, ingen server involverad |
| 5. Skriv ut | `window.print()` bakom en länk; en egen utskriftsvy (oberoende av `IsFocusMode`) i stället för att CSS-styla om den riktiga, interaktiva listan | Inget tillstånd att lagra - en engångshandling. **Verklig bugg hittad under egen verifiering**: utskriftsvyn byggdes först alltid i DOM:en, bara `display:none` på skärmen - Playwright (och `SignUpHelper.SignUpAsync`s eget "sidans enda `<h1>`"-antagande) matchar dolda element precis som synliga, så en andra, dold `<h1>`/uppgiftsnamn hade tyst gjort "det enda elementet med den här texten" falskt för praktiskt taget varje test som besöker Idag - upptäckt genom att köra sviten, inte genom att resonera i förväg. Löst genom att gata hela blocket på `_isPrinting`, satt via `window.matchMedia('print')` (`js/print.js`) - blocket finns inte alls i DOM:en förrän utskrift faktiskt sker | En separat vy byggd med enkla `<li>`, inte `TaskListItem` - svep/expand/knappar har ingen mening på papper |
| 6. Läs upp | En knapp i fokuskortet (`Support/Speech.cs`, Web Speech API) läser namn, rum, tid (om "Visa tid" är på) och beskrivning/steg | Inget tillstånd att spara - talsyntesen är enhetens egen, momentana förmåga, inte en preferens | Autouppläsning, uppläsning utanför fokusläget, en egen röst-/hastighetsinställning - alltid `sv-SE` och 0,95, ett fast värde tills något efterfrågar annat |
| 1. Hur är orken idag? | Tre chips ("Lite"/"Lagom"/"Mycket", `Support/EnergyLevel.cs`, multiplikatorer 0,4/1,0/1,3 på dagens normala budget) anropar `SetAvailabilityAsync` - fanns redan i kontraktet och klienten (se docs/DESIGN.md §6a: den tillfälliga avvikelsen gick redan att sätta via API:t, bara ingen knapp fanns kvar i gränssnittet) - aldrig `SetWeeklyBudgetAsync` | Vilken ETIKETT som valdes är enhetslokal (`Support/EnergyChoice.cs`, `hemordna.energy`) bara så chipen läser rätt tillbaka - servern ser bara de resulterande minuterna, aldrig "orken" eller nivåordet, och absolut inget hushållsgemensamt | Historik över tidigare vald ork, ett fjärde/femte läge, automatisk föreslagen nivå från mönster - bara de tre fasta valen. **Avrundning**: uppdragets egen exempeltext ("60 × 0,4 = 24 min", "60 × 1,3 = 78 min") är avrundad till NÄRMASTE HELA MINUT, inte närmaste 5 som uppdragets prosa också nämner - 24 och 78 är båda redan heltal och ingetdera är en multipel av 5 (närmaste-5 hade gett 25 respektive 80). De konkreta, testbara talen i uppdragets eget exempel vägde tyngre än den lösare formuleringen |

**Uppföljning: batteriikoner på orkenchipparna, och två riktiga buggar hittade av en användare i
produktion.** `Icon.razor` fick tre nya namn (`battery-low`/`-medium`/`-full`, delad
batterikontur + 1/2/3 fyllda staplar) - fast UI-krom, alltid synligt oavsett `ShowIcons`, samma
resonemang som navigeringsflikarnas egna ikoner. Två separata fel upptäcktes från en riktig
skärmbild:

- **"Klart idag"s rumschip visade en genomstrykning rakt igenom pillen** på en användares iPhone
  (Safari/WebKit) - `.task-list-done .task-name`s egen `text-decoration: line-through` målas
  genom en efterföljande `.chip` som standard. Fixat med `text-decoration: none` uttryckligen på
  chippen, samma etablerade, webbläsaroberoende teknik som redan används för just detta problem
  i CSS-communityn i stort. **Kan inte verifieras av testsviten**: Chromium (Playwright:s
  standardwebbläsare här) målar aldrig igenom en `inline-flex`-chip i första hand - bekräftat
  genom att tillfälligt ta bort fixen och jämföra skärmbilder, ingen synlig skillnad i Chromium
  varken med eller utan den. Fixen behålls ändå (korrekt och ofarlig oavsett webbläsare), men
  `MinDagDetailTests` kan bara pinna att radens eget namn har genomstrykningen, inte att chippen
  saknar den.
- **De tre orkenchipparna, nu med ikon, fick inte plats på en rad vid 390px** - "Mycket" blev
  ensam kvar på en egen rad, värre i Stor text. Löst med samma "N knappar delar alltid en rad,
  krymper och radbryter sin EGEN text i stället för att hoppa till en ny rad"-teknik som
  `.level-picker` redan använder (`flex-wrap: nowrap` + `flex: 1 1 0` på varje chip,
  docs/ARCHITECTURE.md §10 - Stor text är ovillkorligt), samt etiketten flyttad till en egen rad
  ovanför knapparna i stället för inline med dem. Ny test `EnergyTests.All_three_energy_chips_
  stay_on_one_row_at_390px` (båda presentationslägena) mäter att alla tre chippens `BoundingBox`
  delar samma Y-position.
- **Samma bugg, samma dag, en annan rad**: `.chips` (`Flytta till en annan dag`/`Extra
  uppgift`/`Ta ledigt idag`) hade exakt samma svaghet - rapporterad av en användare från en
  egen skärmbild direkt efter förra fixen. Löst med samma `flex-wrap: nowrap`, men `flex: 0 1
  auto` snarare än `1 1 0`: till skillnad från de tre kortare, ungefär lika långa orkenetiketterna
  är "Flytta till en annan dag" märkbart längre än de andra två, så lika tredjedelar hade gjort
  den trång och de andra två luftiga i onödan - krymp-till-innehåll låter var och en behålla sin
  egen naturliga bredd tills raden faktiskt tar slut. Ny test
  `AlwaysVisibleChipTests.All_three_day_chips_stay_on_one_row_at_390px` (båda
  presentationslägena), bekräftad att den faktiskt fångar regressionen (körd både med och utan
  fixen).

| Fråga | Varför den väntar |
|---|---|
| Offline-strategi bortom read-only cache | Utanför MVP; får inte låsas in i förväg |

Tidigare på denna lista, nu lösta: vem som genererar `TaskOccurrence` och hur roterande ansvar
räknas ut - se §3 och §5.
