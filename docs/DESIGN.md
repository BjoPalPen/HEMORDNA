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
| Text mjuk | | `#6B7570` | Sekundär text, minuter, områdesetiketter |
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

( ◔ )  25 av 30 min planerade     4 uppgifter · 2 klara · 2 kvar

[x] Torka av köksbänkar    [Kök]           5 min   ⌄
[ ] Dammsug vardagsrum     [Vardagsrum]   10 min   ⌄
[ ] Plocka tvätt           [Tvättstuga]    5 min   ⌄
```

Varje rad: kryssruta, namn, områdeschip, uppskattad tid, expandering.
Sidopanel (dator): uppmuntranskort och **Snabbval** – lägg till tillfällig tid idag, ändra
dagens plan, visa hela hushållets plan.

### Uppgiftsdetalj

Bild, namn, områdeschip, tid, återkommande, beskrivning, ansvarig, växlarna *Kan skjutas
upp* och *Kräver flera personer*. Primär knapp **Markera som klar**, sekundär **Skjut upp**.

### Planering (vecka)

Dagremsa, stapeldiagram över veckans tidsbudget, dagens planerade tid, **Ändra tid idag**.

### Hushållsöversikt

Medlemsavatarer med `använd/budget` minuter, veckans plan som prickmatris per medlem och
dag, områden med antal uppgifter, senaste händelser.

Detta är den enda vyn som visar hela hushållet, och den är aldrig startskärm.

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
