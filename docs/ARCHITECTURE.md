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

### Beslut: `TaskAssignment` och `TaskCompletion` är ännu inte egna entiteter — `PROPOSED`

`TaskOccurrence` bär i dag `AssignedMemberId`, `CompletedByMemberId` och `CompletedAt`
direkt. Den konceptuella kedjan definition → occurrence → assignment → completion är
bevarad, men assignment och completion är fält snarare än rader.

Egna entiteter behövs när något av detta blir ett verkligt krav:

- historik över vem som haft ansvaret (roterande ansvar över tid)
- flera personer på samma occurrence (`RequiresMultiplePeople`)
- delvis slutförande, eller completion-händelser som ska kunna ångras

Uppgraderingen är additiv och kräver ingen ändring av definition/occurrence-gränsen.

### Beslut: `RecurrenceRule` är inte byggd — `PROPOSED`

`TaskDefinition.PreferredWeekday` är den enda schemaläggningshjälp som finns. Full recurrence
(every N weeks, monthly, "tredje tisdagen i månaden") införs när det första verkliga
återkommande use casen kräver det – att bygga motorn i förväg är den mest sannolika källan
till överdesign i det här projektet.

Modellen är förberedd: recurrence hör hemma på `TaskDefinition`, och genereringen av
`TaskOccurrence` går redan via `TaskDefinition.ScheduleFor`.

### Beslut: `MemberPreference` och `NotificationPreference` — `PROPOSED`

Individuell presentation (text / bild+text / stor text / en uppgift åt gången) och
motivationsnivå (Ingen / Lugn) är individuella preferenser, inte hushållsinställningar. De
modelleras som en `MemberPreference` hängd på `HouseholdMember` när presentationslagret byggs.

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

`PROPOSED`: flera hushåll per användare kräver en egen medlemskapstabell. Ändringen är
additiv och går att göra utan att befintlig data blir fel.

---

## 5. Source of truth och synkstrategi — `PROPOSED`

Servern är source of truth. Klienten är aldrig auktoritativ.

Planerad väg:

1. HTTP mot Api för alla skrivningar.
2. SignalR-hub per hushåll för att pusha ändringar till anslutna klienter. Hushållet är den
   naturliga gruppen – klienter joinar gruppen `household:{id}` och får bara det egna
   hushållets händelser, vilket gör isoleringen till samma gräns som i datamodellen.
3. Offline byggs stegvis. Full offline conflict resolution är uttryckligen utanför MVP.

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
| 6 | Occurrence-id stigande | Stabil slutlig tie-break som gör ordningen total |

Regel 1 före regel 2 och 3 är ett medvetet val: en förfallen uppgift kan fortfarande flyttas,
en icke uppskjutbar kan inte det.

### Urval

Greedy first-fit: gå igenom den sorterade listan och ta med allt som får plats i återstående
tid. En lång uppgift som inte får plats blockerar inte de kortare bakom sig.

Kandidater som inte längre är utestående, eller som är schemalagda till ett senare datum,
ingår varken i `Items` eller i `Unplanned` – de tillhör helt enkelt inte dagen.

Utfallet `Unplanned` heter medvetet inte `Deferred`: även en icke uppskjutbar uppgift kan
hamna där, eftersom planeraren inte kan skapa tid. En anropare som vill lyfta fram sådant
filtrerar på `Candidate.CanBeDeferred`.

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
| `GET` | `/api/me` | Den inloggades identitet och hushållstillhörighet |
| `POST` | `/api/households` | `201` med den skapade resursen, `409` om användaren redan har ett hushåll |
| `GET` | `/api/households/{householdId}` | `200`, annars `404` |
| `POST` | `/api/households/{householdId}/members` | `201` med medlemmen |
| `POST` | `/api/households/{householdId}/areas` | `201` med området |
| `GET` | `/api/households/{householdId}/tasks` | `200` med hushållets uppgifter |
| `POST` | `/api/households/{householdId}/tasks` | `201` med uppgiften |
| `POST` | `/api/households/{householdId}/tasks/{taskId}/occurrences` | `201` med den schemalagda instansen |
| `PUT` | `/api/households/{householdId}/members/{memberId}/availability` | `200` med dagens tidsbudget |
| `GET` | `/api/households/{householdId}/members/{memberId}/plan?date=` | `200` med Min dag |

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

## 10. Beslut som ännu inte är fattade — `OPEN`

| Fråga | Varför den väntar |
|---|---|
| Vem eller vad som genererar `TaskOccurrence` (on demand vid planering eller ett schemalagt jobb) | Beror på recurrence-modellen |
| Hur roterande ansvar ska räknas ut | Kräver `TaskAssignment` som egen entitet |
| Offline-strategi bortom read-only cache | Utanför MVP; får inte låsas in i förväg |
