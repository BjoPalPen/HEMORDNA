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
| 5 | Kortare uppgift först | Vid lika ställning: att bli klar slår att påbörja, och mer ryms i budgeten |
| 6 | `ChoreSequenceHint.RankFor` (2026-09-07) | Ett fåtal kända "gör X före Y"-par, se nedan |
| 7 | Occurrence-id stigande | Stabil slutlig tie-break som gör ordningen total |

Regel 1 före regel 2 och 3 är ett medvetet val: en förfallen uppgift kan fortfarande flyttas,
en icke uppskjutbar kan inte det.

**`ChoreSequenceHint`** (regel 6) är en medvetet SMAL nudge, inte ett generellt
städordnings-system - efterfrågat konkret: dammsug (eller sopa) golvet innan man torkar det,
eftersom smuts annars bara flyttas runt. Ren nyckelordsmatchning på uppgiftsnamnet
(`"torka golvet"`/`"moppa"` rankas efter `"dammsug"`/`"sopa golvet"`), tillämpad EFTER allt
planeringsrelevant (uppskjutbarhet, förfallenhet, prioritet, datum, minuter) - kan alltså aldrig
ändra VAD som planeras eller skjuts upp, bara i vilken ordning två annars helt likvärdiga
uppgifter visas när de råkar hamna samma dag. Fler par kan läggas till samma väg om ett
liknande konkret behov dyker upp - ingen anledning att gissa fram en bredare "damma före
dammsug före torka"-ontologi som ingen efterfrågat.

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

`DailyPlanner` har inget begrepp för "rum" i sin egen sortering (den bryr sig om
uppskjutbarhet/förfallenhet/prioritet/datum/minuter) - en dags uppgifter från olika rum
interfolieras därför fritt, vilket i praktiken kändes slumpmässigt (efterfrågat konkret: gör
klart köket innan du går till badrummet, inte köks-uppgift/badrums-uppgift/köks-uppgift om
vartannat). Löst helt klientsidigt i `MinDag.razor`, utan att röra `DailyPlanner`s redan hårt
testade urval/prioritering:

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

---

## 11. Beslut som ännu inte är fattade — `OPEN`

| Fråga | Varför den väntar |
|---|---|
| Offline-strategi bortom read-only cache | Utanför MVP; får inte låsas in i förväg |

Tidigare på denna lista, nu lösta: vem som genererar `TaskOccurrence` och hur roterande ansvar
räknas ut - se §3 och §5.
