---
name: hemordna-review
description: Checklista för egen kodgranskning innan en task i Hemordna rapporteras som klar. Granskar korrekthet, scope, arkitekturgränser, tenant isolation, concurrency, nullable, async, cancellation, loggning, API-kontrakt, migrationspåverkan och onödig komplexitet. Triggers - "granska", "review", "är det klart", "innan commit", "self review", "check the diff", "done".
---

# Hemordna – Self Review

Kör denna innan du rapporterar en task som klar. Granska som en senior reviewer som inte
skrev koden.

```bash
git status --short
git diff --stat
git diff
```

## 1. Korrekthet

- Löser ändringen det faktiska kravet, inte en närliggande tolkning?
- Finns duplicerad logik som redan fanns någon annanstans?
- Edge cases: tom lista, noll minuter, null, förfallet datum, redan slutförd uppgift.
- Är beteendet deterministiskt? Finns någon dold ordningsberoende eller systemklocka?

## 2. Scope

- Är varje ändrad fil relaterad till uppgiften?
- Har något raderats eller skrivits om utan konkret anledning?
- Är något byggt "för framtiden" som inte efterfrågats? (`CLAUDE.md` §12)

## 3. Arkitekturgränser

- Domain fri från EF Core, ASP.NET Core, Npgsql, SignalR, Blazor?
- Application fri från HTTP-concerns och EF-implementation?
- Ingen affärslogik i endpoints eller Razor-komponenter?
- Dependency direction fortfarande enkelriktad?
- Rätt namespace för filens katalog?

## 4. Security och tenant isolation

- Bär varje ny entitet `HouseholdId`?
- Går varje ny query och endpoint att household-scope:a? Är den det redan där det går?
- Finns credentials, tokens eller connection strings i diffen?
- Loggas persondata, lösenord eller tokens?

## 5. Concurrency

- Vad händer om två klienter gör samma sak samtidigt? Dubbel completion?
- Är operationen idempotent där den bör vara?
- Antar koden en enda klient eller att klienten har färsk state?

## 6. C#-hygien

- `nullable` respekterat, inga `!` för att tysta kompilatorn?
- Alla I/O-metoder `async` med `CancellationToken`?
- Inget `async void`, `.Result`, `.Wait()`?
- Ingen bred `catch (Exception)` utanför en boundary? Inga silent failures?
- Loggning på failures, strukturerad?

## 7. API och data

- Exponerar API:t DTO:er, inte EF-entiteter?
- Är API-kontraktet bakåtkompatibelt, eller är brytningen medveten och dokumenterad?
- Migration: läst, icke-destruktiv, verifierad i dev?

## 8. Tester

- Verifierar testerna beteende, inte implementationdetaljer?
- Täcker de invarianter, ogiltig input och statusövergångar?
- Fasta datum, inte dagens datum?
- Finns någon hard-codad genväg i produktionskoden vars enda syfte är att få ett test grönt?
  Ta bort den och fixa domänlogiken.

## 9. Komplexitet

Kan något tas bort utan att kravet går förlorat? Gör det.

## 10. Rapport

Rapportera enligt `CLAUDE.md` §15. Endast faktiskt körd verifiering får kallas `VERIFIED`
(se `implementation-hardening`).
