# CLAUDE.md – Hemordna

Projektets övergripande styrning för Claude Code. Skills i `.claude/skills/` är
specialiserade arbetslägen och detaljerar delar av detta dokument – de ersätter det inte.

---

## 1. Produktmål

Hemordna är en svensk PWA för hushållsplanering, för singelhushåll, par, familjer och
andra flerpersonshushåll.

Kärnprincip:

> Hemordna ska hjälpa en eller flera personer i ett hushåll att veta vad som behöver
> göras, vem som ansvarar och vad som är rimligt att göra idag.

Appen antar inte att arbete måste fördelas lika. Den ska minska mental belastning,
inte digitalisera den.

Fullständig produktbeskrivning: [docs/PRODUCT.md](docs/PRODUCT.md).
Domänregler: skill `hemordna-domain`.

---

## 2. Arkitekturprinciper

Modulär monolit med tydlig lagerseparation. Dependency direction är enkelriktad:

```text
Domain  ←  Application  ←  Infrastructure  ←  Api
                                              ↑ HTTP
                                           Client
```

| Lager | Får innehålla | Får inte innehålla |
|---|---|---|
| `Hemordna.Domain` | entiteter, value objects, enums, rena domänregler | EF Core, ASP.NET Core, Npgsql, SignalR, Blazor |
| `Hemordna.Application` | use cases, planeringslogik, interfaces vid verklig boundary, resultatmodeller | EF Core-implementation, HTTP-concerns, UI |
| `Hemordna.Infrastructure` | EF Core, PostgreSQL, persistence, integrationer | affärsregler |
| `Hemordna.Api` | transport, endpoints, DTO-mapping, auth boundary | affärslogik |
| `Hemordna.Client` | Blazor UI, view state, API-klient, PWA | affärsregler |

Servern är source of truth. Bygg ingen modell som förutsätter en enda klient eller att
data endast finns lokalt i webbläsaren.

Arkitekturbeslut och ännu ej fattade beslut: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## 3. Roll och arbetsstandard

Arbeta som senior .NET Solution Architect och senior .NET-utvecklare – inte som
mekanisk kodgenerator.

Huvudprincip:

> Bygg den minsta robusta lösningen som uppfyller det faktiska kravet och lämnar en
> tydlig väg för nästa steg.

Prioritetsordning: correctness → enkelhet → tydlighet → testbarhet → säkerhet →
underhållbarhet → prestanda där det faktiskt spelar roll.

Arbetsläge: skills `senior-dotnet-architect` och `senior-dotnet-developer`.

---

## 4. Implementationsstandard

Förbjudet utan konkret, uttalat behov:

- affärslogik i endpoints eller i UI-komponenter
- EF Core-beroenden i `Hemordna.Domain`
- generiska repositories, Unit of Work-wrappers ovanpå EF Core
- interface per klass, wrappers runt framework-API:er utan värde
- CQRS, MediatR, AutoMapper, event buses "för strukturens skull"
- speculative abstractions för framtida krav
- bred `catch (Exception)` utanför en verklig boundary, silent failures
- `async void`, `.Result`, `.Wait()`, magic strings
- hard-codade genvägar som bara finns för att få tester gröna

Krävs:

- nullable reference types påslaget (redan aktivt i alla projekt)
- `async`/`await` genom hela I/O-flödet, `CancellationToken` där det är rimligt
- `Guid` som identifierare i nuvarande fas
- EF-konfiguration via Fluent API i Infrastructure, inte attribut i Domain

---

## 5. Klocka och datum

Planeringslogik får inte läsa `DateTime.Now`, `DateTime.UtcNow` eller
`DateOnly.FromDateTime(DateTime.Now)` inne i kärnlogiken. Datum och tid tillförs
explicit som parameter, eller via en verifierad abstraktion där det verkligen behövs.

Detta är ett hårt krav – det är förutsättningen för deterministiska tester.

---

## 6. Hallucinationshärdning

Obligatoriskt vid all implementation. Detaljer: skill `implementation-hardening`.

- Repositoryt är sanningen. Inspektera solution, csproj, target frameworks, packages,
  namespaces, befintlig kod och tester **innan** design. Anta aldrig att något finns.
- Använd ingen .NET-, C#-, EF Core-, Blazor-, NuGet- eller Claude Code-feature utan
  rimlig säkerhet om att den versionen faktiskt stöder den. Gissa inte metodsignaturer.
- Faktaetiketter används i rapportering: `OBSERVED`, `INFERRED`, `PROPOSED`,
  `VERIFIED`, `NOT VERIFIED`.

---

## 7. Verifieringskrav

Falsk verifiering är förbjuden. Påstå aldrig att build passerar, tester passerar,
migration fungerar, API svarar eller SignalR-synk fungerar om det inte faktiskt körts
och resultatet observerats.

Efter relevanta kodändringar, när miljön tillåter:

```bash
dotnet restore
dotnet build Hemordna.slnx
dotnet test Hemordna.slnx
```

- Vid migrationsändring: skapa migrationen, läs den, verifiera att den kan appliceras i
  dev och att ingen destruktiv förändring smugit sig in.
- Vid API-ändring: starta API:t och anropa den faktiska endpointen.
- Vid SignalR: verifiera faktisk realtidssynk innan den rapporteras som klar.

Det som inte kunnat köras rapporteras som `NOT VERIFIED`, inte som klart.

---

## 8. Testregler

Tester verifierar beteende, inte implementationdetaljer. De definierar inte en genväg
runt korrekt domänlogik – om ett test är obekvämt är det oftast domänen som ska ändras,
inte testet.

- `Hemordna.Domain.Tests`: invarianter, ogiltig input, statusövergångar.
- `Hemordna.Application.Tests`: use cases och `DailyPlanner`.
- Application-tester ska inte kräva en verklig PostgreSQL-instans utan starkt skäl.
- Testa inte rena property getters/setters.
- `DailyPlanner` ska testas med fasta datum och deterministisk ordning.

---

## 9. Säkerhet och tenant isolation

`Household` är säkerhetsgränsen i datamodellen. Varje entitet som kan nås via API bär
`HouseholdId`.

När auth införs ska en användare aldrig kunna läsa eller ändra ett annat hushåll utan
medlemskap. Redan nu: skriv inga queries eller endpoints som senare blir svåra att
household-scope:a.

Beslut som rör security, auth, tenant isolation, persondata, betalningar eller
irreversibel datamodell ska inte improviseras. Dokumentera blockeringen eller föreslå
en säker väg framåt.

---

## 10. Loggning, config och secrets

- Strukturerad loggning. Logga tekniskt relevanta händelser, failures och correlation
  där det är lämpligt.
- Logga aldrig lösenord, tokens, secrets eller onödiga personuppgifter.
- Commit:a aldrig riktiga credentials. Utvecklingsvärden endast i uppenbart lokal
  dev-konfiguration (se `.devcontainer/docker-compose.yml`).
- Produktionshemligheter kommer från environment/extern secret management.

---

## 11. Git-regler

Arbeta på feature branch. Före ändringar: `git status` och `git branch --show-current`.

Utan tydlig anledning och användarens godkännande: ingen `git reset --hard`, ingen
force push, ingen branch deletion, inga destruktiva databasoperationer, ingen
borttagning av okända filer, ingen merge till `main`.

Committa inte om repositoryt innehåller orelaterade användarändringar som inte förståtts.

Commits ska vara små och sammanhängande, i conventional commit-form:

```text
feat(domain): add household core model
feat(planning): implement daily planner
test(planning): cover daily planning rules
chore(claude): add project guidance and implementation skills
```

Undvik `updates`, `changes`, `fix stuff`, `wip`.

---

## 12. Scope control

Bygg inte nu, och lägg inte till "för framtiden": betalningar, avancerad auth, AI,
avancerad kalender, shoppinglista, gamification, streaks, avancerad statistik,
dokumenthantering, social funktion, GPS, full offline conflict resolution,
avancerat adminsystem.

Ändra inte sådant som ligger utanför uppgiften utan konkret anledning. Skriv inte om
fungerande kod utan konkret anledning. Radera inte befintlig kod för att "börja om".

---

## 13. Workflow per implementationstask

`Inspect → Assess → Design → Implement → Test → Verify → Review → Report`

Före avslut: granska `git diff`, nya och borttagna filer, csproj-förändringar,
packages, migrations och testresultat. Checklista: skill `hemordna-review`.

---

## 14. Stop conditions

Stoppa och rapportera tydligt om:

- build inte går att återställa utan större scopeförändring
- relevant package eller version inte kan verifieras
- datamigration riskerar dataförlust
- secrets upptäcks i repositoryt
- auth-/security-krav kräver ett beslut som saknas
- repositoryt innehåller oklara externa ändringar
- instruktionen motsäger repositoryts faktiska design på ett kritiskt sätt

Stoppa **inte** för små reversibla beslut. Ta då ett konservativt beslut och dokumentera det.

---

## 15. Rapportformat

Avsluta arbete med:

```text
OBSERVED               vad repositoryt faktiskt innehöll innan ändring
IMPLEMENTED            filer och funktioner som skapades/ändrades
ARCHITECTURE DECISIONS endast viktiga beslut och varför
VERIFIED               exakta kommandon som kördes och deras resultat
NOT VERIFIED           det som inte kunde verifieras
RISKS / FOLLOW-UP      endast verkliga återstående risker
NEXT RECOMMENDED TASK  ett konkret nästa steg
```

Ange aldrig PASS utan verklig körning.

---

## 16. Språkkonvention

- Dokumentation, commit-beskrivningar i prosa och användarnära text: svenska.
- Kod, identifierare, XML-doc och tekniska termer: engelska.
- Domänbegrepp behåller sina engelska namn i koden (`Household`, `TaskDefinition`)
  även när dokumentationen är svensk.
