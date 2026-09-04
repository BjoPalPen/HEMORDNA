---
name: senior-dotnet-developer
description: Arbetsläge för att skriva och ändra kod i Hemordna med produktionsstandard - C#, ASP.NET Core Minimal APIs, Blazor WebAssembly, EF Core, PostgreSQL, SignalR, xUnit. Används vid all implementation av entiteter, use cases, endpoints, persistence, UI-komponenter och tester. Triggers - "implementera", "lägg till", "skriv koden", "fixa", "refaktorera", "implement", "add endpoint", "write the code", "refactor".
---

# Senior .NET Developer – Hemordna

Produktionsstandard, inte demokod. Stack: .NET 10, C# senaste språkversion via SDK,
nullable påslaget i alla projekt, xUnit.

Läs `CLAUDE.md` §4 (implementationsstandard) och §5 (klocka) innan du skriver kod.

## Placering – var hör koden hemma

| Det du skriver | Projekt |
|---|---|
| Entitet, value object, enum, invariant | `Hemordna.Domain` |
| Use case, planeringslogik, resultatmodell, boundary-interface | `Hemordna.Application` |
| `DbContext`, `IEntityTypeConfiguration<T>`, migration, DI-extension | `Hemordna.Infrastructure` |
| Endpoint, request/response-DTO, mapping, auth | `Hemordna.Api` |
| Razor-komponent, view state, API-klient, PWA | `Hemordna.Client` |

Hamnar logik på fel ställe, flytta den – lägg inte till ett lager till.

## Hårda regler

**Förbjudet**
- affärslogik i endpoints eller Razor-komponenter
- EF Core, ASP.NET Core, Npgsql, SignalR eller Blazor-referenser i `Hemordna.Domain`
- generiska repositories, Unit of Work-wrappers ovanpå EF Core, interface per klass
- wrappers runt framework-API:er som inte tillför något
- CQRS/MediatR/AutoMapper/event bus utan konkret problem att lösa
- bred `catch (Exception)` utanför en verklig boundary; silent failures
- `async void`, `.Result`, `.Wait()`, statiska service locators, magic strings
- speculative abstractions – inget "för framtiden"
- hard-codade specialfall som bara finns för att ett test ska bli grönt

**Krav**
- nullable respekteras; inga `!`-operatorer för att tysta kompilatorn
- alla I/O-metoder är `async` och tar `CancellationToken`
- domänentiteter validerar sina invarianter i konstruktor/metoder och exponerar
  `IReadOnlyCollection<T>`, inte muterbara listor
- `Guid` som id i nuvarande fas
- EF-konfiguration via Fluent API i Infrastructure, inte attribut i Domain
- API exponerar DTO:er, aldrig EF-entiteter
- strukturerad loggning; aldrig lösenord, tokens eller secrets i loggen

## Tid och datum

Ingen `DateTime.Now` / `DateTime.UtcNow` / `DateOnly.FromDateTime(DateTime.Now)` i
domän- eller planeringslogik. Datum och tidpunkter skickas in som parametrar. Detta är
kravet som gör `DailyPlanner` testbar.

## Tester hör till implementationen

En ändring är inte klar utan test som verifierar beteendet. Testa invarianter,
statusövergångar och ogiltig input – inte property getters. Fasta datum, aldrig dagens datum.

## Innan du säger att du är klar

```bash
dotnet build Hemordna.slnx
dotnet test Hemordna.slnx
```

Kördes de inte, är arbetet `NOT VERIFIED`. Se `implementation-hardening`.
Granska sedan enligt `hemordna-review`.
