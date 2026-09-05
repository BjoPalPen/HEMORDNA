# Överlämning

Lägesbild per 2026-09-05, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

`main` är på `2abb539`, opåverkad. Allt arbete ligger på `feat/tidsbudget-per-medlem`, med
öppen PR #9 mot `main` (ej mergad). Tid: se §6a/§6b i DESIGN.md - fortfarande dold i Min
dag/Planering, satt via roll (Hushållsöversikten) eller rumsmallens våningsguide (Områden).

**"Uppgifter" som egen sida är borttagen.** All uppgiftshantering flyttade in i Områden
(feedback: kändes fel att hantera uppgifter någon annanstans än rummet de hör till). Varje
rum är nu ett eget kort med sin uppgiftslista, en "Ta bort"-knapp per uppgift och en dold
"Lägg till en uppgift i `<rum>`"-form; ett "Övrigt"-kort tar arealösa uppgifter. Ny use case
`DeactivateTaskDefinition` + `DELETE .../tasks/{id}` gör borttag av en enskild, redan skapad
uppgift möjligt - tidigare fanns bara hela-rummet-bort. Se `RoomTasks.razor` (ny komponent).
NavMenu/Mer uppdaterade: Områden tog Uppgifters plats i mobilens bottenrad.

**Sovrum kan ägas av en medlem** - vid "Sovrum" i våningsguiden väljs per instans ("Sovrum 1",
"Sovrum 2", ...) en ägare eller "Delat ansvar"; en ägare ger `HasRotatingResponsibility=false`
tillsammans med `DefaultResponsibleMemberId`. Ny komponent `BedroomOwnerPicker.razor` - bara
för sovrum, inte andra rumstyper. Tidsuppskattningar i `RoomTemplates` är omkalibrerade nedåt
(t.ex. dammsugning 10→3-5 min) efter feedback om orimligt höga siffror.

**Playwright-lärdom:** `@bind` på `<input type=number>` committar bara på blur, inte
`FillAsync`. Utlöser blur (via nästa klick) en re-render som flyttar submit-knappen (fler
rader läggs till), kan Playwrights redan beräknade klick-koordinat missa - inget fel, klicket
uträttar bara ingenting. Fix: `Keyboard.PressAsync("Tab")` + vänta in layouten före klicket.
Inte en produktbugg.
195 tester gröna (Domain 69, Application 90, E2E 36).

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**PostgreSQL på port `5433`, inte `5432`** (`.env`). Migrationer: `dotnet ef database update`
med samma `ConnectionStrings__Hemordna` som API:t.

**LAN-åtkomst:** binda med `--urls "http://*:PORT"`, inte `0.0.0.0` - det öppnar bara IPv4.

## Kända brister

PWA:n är overifierad i riktig browser. `HushallTests.Changing_a_members_role_...` är flaky
under full parallell körning (passerar isolerat) - misstänkt timing, ej åtgärdad.

## Öppna frågor och nästa steg

Väckt men **inte påbörjad**: uppskatta städbehov utifrån antal rum/medlemmar/husdjur.
**Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll (ARCHITECTURE.md §4).
