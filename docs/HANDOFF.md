# Överlämning

Lägesbild per 2026-09-08, för en ny session. Arbetssättet styrs av
[../CLAUDE.md](../CLAUDE.md), som gäller före detta. Max 50 rader; äldre lägesbilder ligger i
[handoff/](handoff/), se CLAUDE.md §17.

## Läge

**"Ny form" (steg 1-5) är mergat och kör i produktion** - `https://app.hemordna.se`, Hetzner
`62.238.45.45` (delad Caddy/nätverk med BowlingPlatform). `main`s senaste commit är `3f0c426`;
flera mindre uppföljningar landade också sen merget - se `git log`/ARCHITECTURE.md §10, inte
denna fil.

**"Ny form 2026" är klart, alla sex delsteg** - ett andra visuellt delta (svävande navpill,
kant-till-kant-blur, scroll-rubrik, ark med två höjdlägen, squircle/kantlösa ytor, fjädrande
bekräftelse), se ARCHITECTURE.md §10 "Beslut: Ny form 2026" för beslut och buggar per delsteg.
Sex commits på `feat/ny-form-2026`, **inget mergat till `main` utan uttryckligt godkännande**.

**Tre produktionsbuggar från första driftsättningen, redan fixade** (se `handoff/`):
`ContentTypeProvider` för `.dat`/`.blat`/`.wasm`, produktionens egen
`appsettings.Production.json`, delad Docker-tjänst döpt om `api` → `hemordna-api`.

## Köra

Fullständig uppstart: [../README.md](../README.md). Portar: API `5199`, klient `5200`.
**PostgreSQL på port `5433`, inte `5432`** (`.env`). **LAN-åtkomst:** binda med
`--urls "http://*:PORT"`, inte `0.0.0.0`.

## Fällor som kostat tid

Blazor CSS-isolering döper om `@keyframes`-identifierare, inte bara selektorer
(`task-spring` → `task-spring-b-xxxxxxxx`) - ett E2E-test som kollar `animationName` måste
matcha på prefix, inte exakt namn. `::deep` måste stå FÖRE hela den del av en selektor som
inte hör till komponentens eget renderträd (t.ex. `html[data-scrolled]`), inte bara före
målklassen - annars försöker isoleringen lägga sitt scope-attribut på `html`, vilket aldrig
matchar. `SheetDetent.Half` råkar vara enumens nollvärde, samma som ett osatt fälts egen
default - synka alltid en sådan parameter i `OnParametersSet`, inte `OnAfterRenderAsync` (som
kör efter den första renderingen som redan behöver värdet).

## Kända brister

Ett dokumenterat, medvetet ej fixat race: att välja en roll skickar två samtidiga PUT (roll +
veckobudget) - budgeten kan under belastning tappas trots att rollen sätts. Synligt i
`HushallTests`/`SkarmbilderTests` som en retry-loop.

## Öppna frågor och nästa steg

**Nästa, väntar på uttryckligt godkännande:** merga `feat/ny-form-2026` till `main` och
driftsätta. **Beslut, inte öppen fråga:** en användare tillhör exakt ett hushåll
(ARCHITECTURE.md §4).
