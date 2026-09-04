---
name: hemordna-domain
description: Hemordnas produkt- och domänregler - hushall, medlemmar, tidsbudget, omraden, uppgifter, occurrences och planering. Anvands nar domanmodellen ska andras eller utokas, nar en produktregel ska tolkas, eller nar det ar oklart var ett begrepp hor hemma. Triggers - "Household", "HouseholdMember", "TaskDefinition", "TaskOccurrence", "DailyPlanner", "tidsbudget", "recurrence", "min dag", "hushall", "uppgift", "domanmodell", "domain model".
---

# Hemordna – Domän

Bevarar produktens regler mellan sessioner. Full produktbeskrivning:
`docs/PRODUCT.md`. Arkitektur och beslutsstatus: `docs/ARCHITECTURE.md`.

## Produktens viktigaste princip

> Hemordna ska hjälpa en eller flera personer i ett hushåll att veta vad som behöver göras,
> vem som ansvarar och vad som är rimligt att göra idag.

Följdregler som ofta glöms:

- Arbete måste **inte** fördelas lika. Modellera aldrig rättvisa som ett krav.
- Ett hushåll kan ha **en** person. Ingenting får förutsätta två eller fler.
- Huvudskärmen är den egna dagen, inte hushållets backlogg.
- Inget skuldbeläggande språk, inga jämförelser mellan medlemmar, inga streaks eller poäng.
- Presentation och motivationsnivå är **individuella** preferenser, inte hushållsinställningar.

## Modellen som finns i dag

```text
Household (tenant- och säkerhetsgräns)
├── HouseholdMember ── WeeklyTimeBudget (value object, minuter per veckodag)
│        └── MemberAvailability   (undantag för ETT datum)
└── Area

TaskDefinition ──ScheduleFor──► TaskOccurrence
```

| Typ | Regel som inte får brytas |
|---|---|
| `Household` | Tenant-gräns. Allt klientnåbart bär `HouseholdId`. Namn på medlemmar och områden är unika inom hushållet |
| `HouseholdMember` | Deaktiveras, raderas aldrig – historik måste peka på en verklig medlem |
| `WeeklyTimeBudget` | Immutable. Noll minuter en veckodag är giltigt och betyder "ingen tid" |
| `MemberAvailability` | Gäller ett datum. Ändrar aldrig veckobudgeten |
| `Area` | Behöver inte vara ett rum – "Hund" och "Trädgård" är giltiga områden |
| `TaskDefinition` | Beskriver **normen**. `EstimatedMinutes > 0` |
| `TaskOccurrence` | Bär allt **tillfälligt**. Snapshottar minuter, prioritet och deferability |
| `TaskOccurrenceStatus` | `Planned` / `Completed` / `Skipped`. Uppskjutning är *inte* en status |

## De tre reglerna som styr det mesta

1. **Definition kontra occurrence.** Ändra aldrig `TaskDefinition` för att uttrycka något
   tillfälligt – bortrest medlem, en uppgift som hoppas över en gång, en tillfällig
   omfördelning. Allt sådant hör hemma på occurrence-nivå. Kedjan
   `TaskDefinition → TaskOccurrence → assignment → completion` ska hållas konceptuellt intakt
   även när de två sista i dag bara är fält på occurrencen.

2. **Snapshot vid schemaläggning.** `TaskOccurrence` kopierar `EstimatedMinutes`, `Priority`
   och `CanBeDeferred` från definitionen. En senare redigering av definitionen får aldrig i
   efterhand skriva om arbete som redan ligger på någons dag.

3. **Uppskjutning flyttar datum.** `DeferTo` flyttar `ScheduledDate` framåt medan
   `OriginalScheduledDate` ligger kvar, så uppskjutning kan inte dölja att något är försenat.
   Statusen förblir `Planned`.

## Tidsbudget

Tidsbudgeten är inte metadata – den är planeringens huvudsakliga input.

Tillgänglig tid en dag löses av `HouseholdMember.AvailableMinutesOn(date, override)`:
undantaget för datumet om det finns, annars veckobudgeten. "Mindre tid idag" och "ingen tid
idag" får aldrig förstöra den normala veckan.

## DailyPlanner

`Hemordna.Application.Planning.DailyPlanner` är en ren, deterministisk funktion utan
dependencies och utan klocka. Datum och tillgängliga minuter skickas alltid in.

Sortering, i ordning: **icke uppskjutbara → förfallna → högre prioritet → äldst
förfallodatum → kortare först → occurrence-id**. Sedan greedy first-fit; en lång uppgift som
inte får plats blockerar inte de kortare bakom sig.

Ändras någon av dessa regler ska tabellen i `docs/ARCHITECTURE.md` §6 uppdateras i samma
ändring, och motiveringen skrivas ut.

## Ännu inte byggt – bygg det inte i förväg

`RecurrenceRule`, `TaskAssignment` och `TaskCompletion` som egna entiteter,
`MemberPreference`, `NotificationPreference`. Alla är `PROPOSED` i `docs/ARCHITECTURE.md`
med villkoret för när de ska införas. Recurrence-motorn är projektets mest sannolika
överdesign – bygg den när ett verkligt use case kräver den, inte innan.

Relaterat: `senior-dotnet-architect`, `senior-dotnet-developer`, `hemordna-review`.
