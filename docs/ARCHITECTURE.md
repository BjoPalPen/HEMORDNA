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

**Hur roterande ansvar räknas ut (löser §10):** `RotationPicker` cyklar genom hushållets
aktiva medlemmar, ordnade efter `CreatedAt` och sen `Id`. Utan tidigare tilldelning används
`DefaultResponsibleMemberId` om satt, annars den som gick med först.

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

### Beslut: `MemberPreference` — `IMPLEMENTED` (domän och API) / `PROPOSED` (UI)

Individuell presentation (`PresentationMode`: text / bild+text / stor text / en uppgift åt
gången, plus `ImageOnly`/`ReadAloud` förberedda) och motivationsnivå (`MotivationLevel`: Ingen
/ Lugn) finns som `MemberPreference`, en rad per medlem, satt via
`PUT .../members/{id}/preferences`. En egen inställningssida i klienten är medvetet
uppskjuten - se HANDOFF.md.

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
| Offline-strategi bortom read-only cache | Utanför MVP; får inte låsas in i förväg |

Tidigare på denna lista, nu lösta: vem som genererar `TaskOccurrence` och hur roterande ansvar
räknas ut - se §3 och §5.
