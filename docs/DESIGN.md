# Hemordna – Design

Visuell riktning och gränssnittsprinciper. Fastställd utifrån mockup 2026-09-04.

Produktreglerna som styr designen finns i [PRODUCT.md](PRODUCT.md) – särskilt §4 (Min dag
som huvudskärm), §7 (individuell presentation) och §8 (motivation utan skuldbeläggning).

---

## 1. Känsla

> Ett enklare hem, en lugnare vardag.

Gränssnittet ska kännas **lugnt, luftigt och vänligt** – inte som ett produktivitetsverktyg.
Mjuka rundade hörn, generös luft, låg kontrast i det som inte är viktigt, tydlig kontrast i
det som är. Inga hårda skuggor, inga larmfärger för normala tillstånd.

Designen får aldrig få användaren att känna sig sen, granskad eller jämförd.

---

## 2. Färger

| Roll | Namn | Värde | Används till |
|---|---|---|---|
| Primär | Lugn grön | `#4E9D74` | Knappar, aktiv navigation, progress, bockar |
| Primär stark | | `#3C7E5B` | Hover, tryckt läge |
| Primär mjuk | | `#E8F3EC` | Aktiv navigationsrad, mjuka fyllningar |
| Sekundär | Varm beige | `#E5DAC8` | Sekundära ytor, chips |
| Sekundär mjuk | | `#F5EFE5` | Kortbakgrund med värme |
| Accent | Mjuk blå | `#5B8CC4` | Informationsmarkeringar, sekundära ikoner |
| Varning | Varm orange | `#E07A5F` | Endast för faktiska problem – aldrig för "sent" |
| Bakgrund | Ljus och luftig | `#FBFAF7` | Sidbakgrund |
| Yta | | `#FFFFFF` | Kort och paneler |
| Text | | `#2C3330` | Brödtext och rubriker |
| Text mjuk | | `#6B7570` | Sekundär text, lägesetiketter, områdesetiketter |
| Ram | | `#E6E4DE` | Kortkanter, avdelare |
| Logotyp | Skifferblå | `#3F6191` | Endast logotypen |

Varningsfärgen är medvetet varm och används sparsamt. En försenad uppgift är **inte** ett
fel – den markeras med ord ("sedan i tisdags"), inte med rött.

Alla färgpar ska klara WCAG AA för text.

## 3. Form

- Radie: `12px` på kort, `10px` på knappar och fält, `999px` på chips och avatarer.
- Skugga: mycket subtil, `0 1px 2px rgba(44,51,48,.06)`. Djup skapas med ramar och luft.
- Avstånd bygger på en 4px-skala: 4, 8, 12, 16, 24, 32, 48.

## 4. Typografi

Systemtypsnitt – snabbt, lokalt, inga externa anrop.

| Roll | Storlek | Vikt |
|---|---|---|
| Sidrubrik ("Hej Anna!") | 28–32px | 600 |
| Sektionsrubrik | 18px | 600 |
| Brödtext | 15px | 400 |
| Sekundär | 13px | 400 |

Stor text-läget (se §7) skalar upp bastexten – det är inte en egen typografi.

---

## 5. Tonläge

Rubriken hälsar personen vid namn. Underrubriken tar udden av kravet.

Tillåtet:

- "Hej Anna! 👋"
- "Här är dina uppgifter för idag. En sak i taget räcker."
- "Det viktigaste är gjort."
- "Dagens uppgifter är klara."
- "Vill du fortsätta där du slutade?"

Förbjudet, oavsett hur det formuleras:

- "Du ligger efter", "Du missade", "streak", "X dagar i rad"
- Jämförelser mellan hushållsmedlemmar
- Röda siffror för obetalda "skulder" i tid

Försenade uppgifter beskrivs neutralt och sakligt, aldrig anklagande.

---

## 6. Skärmar

### Min dag (huvudvy)

Startskärmen. Visar **bara den egna dagen** – aldrig hushållets backlogg.

```text
Hej Anna! 👋
Här är dina uppgifter för idag. En sak i taget räcker.

4 uppgifter · 2 klara · 2 kvar

[x] Torka av köksbänkar    [Kök]      ⌄
[ ] Dammsug vardagsrum     [Vardagsrum]   ⌄
[ ] Plocka tvätt           [Tvättstuga]   ⌄
```

Varje rad: kryssruta, namn, områdeschip, expandering. Ingen tid visas – varken per uppgift
eller som summa. Tid hanteras i bakgrunden (se §6a); användaren ser bara namn och bock.
Sidopanel (dator): uppmuntranskort och **Snabbval** – ett kvalitativt lägesval (Ingen/Lite/
Lagom/Gott om tid) för dagens tillfälliga avvikelse, och **Din vanliga vecka**: en rollknapp
(se §6b) sätter hela veckan i ett klick, med dag-för-dag-redigeraren undangömd bakom
"Anpassa varje dag för sig" för den som verkligen behöver avvika från rollen.

### Uppgiftsdetalj

Bild, namn, områdeschip, återkommande, beskrivning, ansvarig, växlarna *Kan skjutas
upp* och *Kräver flera personer*. Primär knapp **Markera som klar**, sekundär **Skjut upp**.
Ingen tid visas; uppskattad tid sätts som ett kvalitativt läge (se §6a) och lever bara i
domänen.

### Planering (vecka)

**Min vecka**: sju rader, en per veckodag, med bara ett kvalitativt läge i text (t.ex.
"Ingen tid", "Lagom tid") – inget stapeldiagram, inga minuter. **Idag**-kortet erbjuder
samma fyra lägesknappar som Min dag för att ändra just dagens avvikelse.

### Hushållsöversikt

Medlemsavatarer (namn, ingen `använd/budget`-siffra), veckans plan som prickmatris per
medlem och dag, områden med antal uppgifter, senaste händelser.

Detta är den enda vyn som visar hela hushållet, och den är aldrig startskärm.

### Områden

Lista över hemmets delar. Formuläret "Lägg till rum från mall" (se §6b) är förstahandsvägen;
ett tomt, namnlöst område ligger bakom disclosuren "Lägg till ett tomt område i stället", för
grupperingar som inte är ett rum (t.ex. "Hund", "Garage"). Varje område visar sitt antal
uppgifter.

### 6a. Tid hanteras i bakgrunden, visas aldrig

Domänen räknar fortfarande i minuter (uppskattad tid, veckobudget, `availableMinutes` från
API:t) – det är vad `RecurrenceRule`, `DailyPlanner` och rotationslogiken behöver för att
räkna ut vad som får plats en given dag. Men inget UI-lager visar den siffran. Klienten
mappar minuter till fyra kvalitativa lägen (`Hemordna.Client.Support.TimeLevel`: Ingen tid/
Lite tid/Lagom tid/Gott om tid → 0/15/30/60 min) och visar bara läget, aldrig talet.

Bakgrund: alltför mycket tidsvisning (minuträknare, progress-ringar, stapeldiagram) skapar
stress snarare än lugn – motsatsen till appens syfte. Uppgiften och bocken räcker; tiden är
ett internt planeringsverktyg, inte något användaren ska behöva förhålla sig till.

### 6b. Roller och rumsmallar – färre val vid start

Även fyra kvalitativa lägen per veckodag var för många beslut på en gång (produktfeedback).
`Hemordna.Client.Support.HouseholdRolePresets` erbjuder tre roller istället – **Vuxen, jobbar
heltid**, **Barn eller ungdom**, **Pensionär / hemma dagtid** – och räknar ut en rimlig
vardag/helg-fördelning åt medlemmen i ett enda val, både vid "Lägg till medlem" och i "Din
vanliga vecka". Dag-för-dag-redigeringen finns kvar som ett undangömt avancerat alternativ
för den som inte passar någon roll.

På samma sätt genererar `RoomTemplates` en färdig checklista av vanliga städuppgifter när
någon namnger vilken typ av rum de lägger till (t.ex. "Litet wc" ger handfat, toalettstol,
spegel, hyllor, golv) i stället för att användaren ska hitta på och skriva in varje uppgift
för hand. Genererade uppgifter upprepas varje vecka och roterar mellan hushållets medlemmar,
samma mönster som redan används för den seedade uppgiften "Dammsug vardagsrum".

### Inställningar – Min visning

Se §7. Skärmen avslutas med raden:

> Detta är din personliga inställning och påverkar inte andra i hushållet.

---

## 7. Presentationslägen

Individuell preferens, aldrig en hushållsinställning.

| Läge | Status |
|---|---|
| Text (standard) | MVP |
| Bild + text | MVP |
| Stor text | MVP |
| En uppgift åt gången | MVP |
| Endast bild | Senare |
| Uppläsning | Senare |

Lägena ska byta *presentation* av samma data – inte vilken data som visas.

---

## 8. Navigation

| Yta | Mönster |
|---|---|
| Dator | Vänster sidopanel: Min dag, Uppgifter, Områden, Planering, Hushåll, Inställningar. Användarkort längst ned |
| Platta | Sidopanel eller topprad beroende på bredd |
| Mobil | Bottenrad: Min dag, Uppgifter, Planering, Mer |

Min dag är alltid första valet och startvyn.

---

## 9. Logotyp

Ett hus omgivet av en bladkrans, i skifferblått `#3F6191`. Signalerar hem och lugn, inte
effektivitet.

Ligger i `src/Hemordna.Client/wwwroot/brand/`. Används i sidhuvud och som app-ikon.
Byt aldrig färg på märket; placera det på ljus bakgrund.

Ordbild: **Hemordna**, med underraden *Ett enklare hem, en lugnare vardag*.

---

## 10. Tillgänglighet

- Kontrast enligt WCAG AA.
- Kryssrutor och knappar minst 44×44px träffyta.
- Färg är aldrig ensam bärare av betydelse – status har alltid text eller ikon.
- Fokusmarkering syns tydligt och tas aldrig bort.
- Stor text-läget får inte bryta layouten.
