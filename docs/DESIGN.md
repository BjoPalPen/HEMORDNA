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
eller som summa, och inget val om tid över huvud taget. Tid hanteras helt i bakgrunden (se
§6a); användaren ser bara namn och bock. Sidopanel (dator): bara ett uppmuntranskort, och
bara när det finns något avklarat att uppmuntra – annars ingen sidopanel alls. Ett tidigare
"Snabbval" för dagens tillfälliga avvikelse är borttaget: även ett litet, kvalitativt tidsval
visade sig kännas rörigt och stressande på sidan alla öppnar varje dag (produktfeedback).

### Uppgiftsdetalj

Bild, namn, områdeschip, återkommande, beskrivning, ansvarig, växlarna *Kan skjutas
upp* och *Kräver flera personer*. Primär knapp **Markera som klar**, sekundär **Skjut upp**.
Ingen tid visas; uppskattad tid sätts som ett kvalitativt läge (se §6a) och lever bara i
domänen.

### Planering (vecka)

**Min vecka**: sju rader, en per veckodag, med bara ett kvalitativt läge i text (t.ex.
"Ingen tid", "Lagom tid") – inget stapeldiagram, inga minuter, och ingen redigering här.
Helt läsläge; rollen (se §6b) är enda sättet att ändra veckan.

### Hushållsöversikt

Medlemsavatarer (namn, ingen `använd/budget`-siffra) med en rollväljare per medlem (se §6b) -
en roll sätter hela veckans budget i ett val, utan siffror. Vidare: veckans plan som
prickmatris per medlem och dag, områden med antal uppgifter, senaste händelser.

Detta är den enda vyn som visar hela hushållet, och den är aldrig startskärm - därför är den
också platsen för roll-/tidsinställningar som inte alla medlemmar behöver se eller röra vid,
till skillnad från Min dag som alla öppnar varje dag.

### Områden

Hemmets rum, och all uppgiftshantering, samlat på ett ställe - det finns ingen egen
"Uppgifter"-sida längre (produktfeedback: kändes konstigt att hantera uppgifter någon
annanstans än i rummet de redan hör till). Formuläret "Lägg till en våning" (se §6b) är
förstahandsvägen till nya rum; ett tomt, namnlöst område ligger bakom disclosuren "Lägg till
ett tomt område i stället", för grupperingar som inte är ett rum (t.ex. "Hund", "Garage").

Varje befintligt rum är ett eget kort: namn, antal uppgifter och total uppskattad tid, en
"Ta bort rum"-knapp, listan över rummets egna uppgifter (var och en med sin frekvens och en
egen "Ta bort"-knapp), och en dold "Lägg till en uppgift i `<rum>`"-disclosure för att lägga
till fler för hand. Ett sista kort, "Övrigt", samlar uppgifter som inte hör till något
särskilt rum. Att kunna ta bort en enskild uppgift - inte bara hela rummet - är i sig ett
direkt svar på feedback: annars fanns inget sätt att bli av med en uppgift i ett redan
skapat rum.

### 6a. Tid hanteras i bakgrunden, visas aldrig

Domänen räknar fortfarande i minuter (uppskattad tid, veckobudget, `availableMinutes` från
API:t) – det är vad `RecurrenceRule`, `DailyPlanner` och rotationslogiken behöver för att
räkna ut vad som får plats en given dag. Men inget UI-lager visar den siffran. Klienten
mappar minuter till fyra kvalitativa lägen (`Hemordna.Client.Support.TimeLevel`: Ingen tid/
Lite tid/Lagom tid/Gott om tid → 0/15/30/60 min) och visar bara läget, aldrig talet.

Bakgrund: alltför mycket tidsvisning (minuträknare, progress-ringar, stapeldiagram) skapar
stress snarare än lugn – motsatsen till appens syfte. Uppgiften och bocken räcker; tiden är
ett internt planeringsverktyg, inte något användaren ska behöva förhålla sig till. Det gäller
även ett litet, kvalitativt val: dagens tillfälliga avvikelse (`availableMinutes`) går
fortfarande att sätta via API:t, men har ingen knapp någonstans i gränssnittet längre - även
det visade sig kännas som "tid som ett val".

### 6b. Roller och rumsmallar – färre val vid start

Även fyra kvalitativa lägen per veckodag var för många beslut på en gång (produktfeedback).
`Hemordna.Client.Support.HouseholdRolePresets` erbjuder tre roller istället – **Vuxen, jobbar
heltid**, **Barn eller ungdom**, **Pensionär / hemma dagtid** – och räknar ut en rimlig
vardag/helg-fördelning åt medlemmen i ett enda val.

Rollvalet sätts vid "Lägg till medlem" och kan ändras därefter från en liten rollväljare per
medlem på Hushållsöversikten - inte på Min dag. Uppföljande feedback: att visa och kunna ändra
en roll är i sig "tid som ett val", och det behöver inte alla medlemmar se eller ta ställning
till varje gång de öppnar appen. Hushållsöversikten är redan en sida ingen är tvungen att
besöka dagligen, till skillnad från Min dag, så den är rätt plats för den här typen av
inställning. `HouseholdRolePresets.Match` känner igen om en medlems sparade budget kommer
från en roll eller är satt för hand (då visas "Anpassad tid" i väljaren i stället).

På samma sätt genererar `RoomTemplates` en färdig checklista av vanliga städuppgifter när
någon namnger vilken typ av rum de lägger till (t.ex. "Litet wc" ger handfat, toalettstol,
spegel, hyllor, golv) i stället för att användaren ska hitta på och skriva in varje uppgift
för hand. Genererade uppgifter upprepas varje vecka och roterar mellan hushållets medlemmar,
samma mönster som redan används för den seedade uppgiften "Dammsug vardagsrum".

Formuläret på Områden går längre än ett rum i taget: en valfri **våning** (fritext, t.ex.
"Våning 1") kan konfigureras med flera rumstyper på en gång, var och en med ett **antal**,
så tre sovrum skapas i ett svep i stället för att formuläret fylls i tre gånger. Fler än ett
av samma typ numreras ("Sovrum 1", "Sovrum 2", ...); en angiven våning blir en prefix på
varje rums namn ("Våning 1 – Kök"). Direkt efter skapandet visas en sammanfattning per rum
med `RoomTemplate.TotalMinutes` och en totalsumma - ett medvetet undantag från §6a: under
planering av hemmet är frågan "hur lång tid tar det här rummet?" rimlig att svara på med en
siffra, till skillnad från den dagliga vyn där samma siffra bara stressar.

Varje uppgift i en vald rumstyp listas som en kryssruta, **förvald**, direkt under raden - inte
för att beslutet ska tas varje gång (mallens urval räcker för de allra flesta), utan för att
kunna plocka bort det enstaka som inte stämmer (ett badrum utan badkar behöver inte "Skrubba
dusch eller badkar"). Att bocka ur en uppgift utesluter den helt - den skapas aldrig, snarare
än att skapas och sen behöva tas bort.

Varje mallad uppgift får också en **frekvens** - Daglig, **Två gånger i veckan**, Veckovis,
Månadsvis eller **Vid behov** - satt som ett förvalt, rimligt standardvärde per uppgift i
`RoomTemplates` (t.ex. "Diska" dagligen, "Rengör toalettstolen" varje vecka, "Damma hyllor" vid
behov), i stället för att den som lägger till rummet ska behöva svara på det för var och en av
upp till åtta uppgifter. "Två gånger i veckan" (t.ex. dammsugning i sovrummet) har ingen egen
kalenderplats - en veckas återkommelse bär bara en veckodag - så den byggs som en daglig regel
med tre dagars intervall (`RoomTemplateTask.ToScheduling`), vilket i praktiken glider över
veckans dagar snarare än att alltid landa på exakt samma två. "Vid behov" är inte en
kalendercykel: uppgiften dyker upp igen ett fast antal dagar (`RoomTemplateTask.
AsNeededDefaultDays`, 21) efter den senast blev avklarad, inte på ett givet datum - se
ARCHITECTURE.md §3 för `TaskDefinition.StaleAfterDays`. Samma frekvensval finns även i det
manuella "Lägg till en uppgift i `<rum>`"-formuläret på Områden.

Sovrumsmallen speglar en verklig städrytm snarare än en enda uppskattning: bädda sängen och
vädra rummet dagligen, dammsugning två gånger i veckan, torka golvet varje vecka, och torka
lister/tvätta fönster en gång i månaden.

En mallad uppgift kan också märkas **endast vuxna** (`RoomTemplateTask.AdultsOnly`,
`TaskDefinition.RequiresAdult`) - sovrumsmallens "Tvätta fönster" är det första exemplet, då
stegar och högre höjd inte är en barnuppgift. Rotationen (`RotationPicker`) hoppar då över
medlemmar vars roll är "Barn eller ungdom"; är alla aktiva medlemmar barn faller den tillbaka
till hela hushållet snarare än att låta uppgiften stå utan någon ansvarig. Samma kryssruta
("Bara vuxna ska tilldelas den här") finns i det manuella formuläret. Rollen är numera en
sparad egenskap på medlemmen (`HouseholdMember.Role`), satt när rollen väljs på
Hushållsöversikten - inte längre bara härledd genom att jämföra sparad veckobudget mot en
rollmall.

"Övrigt"-kortet erbjuder också ett eget bibliotek av vanliga, rumslösa hushållssysslor
(`GeneralTaskTemplates`: handla mat, tvätta, betala räkningar, rasta hunden, ...) bakom
disclosuren "Lägg till vanliga hushållssysslor" - samma mall-tänk som `RoomTemplates`, men
**inget är förvalt**, eftersom vilka som är relevanta varierar mycket mer mellan hushåll än
vilka uppgifter ett givet rum har.

**Rum och medlemmar kan tas bort**, liksom enskilda uppgifter. Ett rum kan ha skapats fel,
eller en medlem kan ha flyttat. Knapparna heter "Ta bort" och avaktiverar
(`Area`/`HouseholdMember`/`TaskDefinition.Deactivate`) snarare än raderar - historiken
(avklarade uppgifter, vem som gjorde vad) pekar fortfarande på en riktig rad. Att ta bort ett
rum avaktiverar även rummets egna uppgifter, så en borttagen "Sovrum 2" inte fortsätter dyka
upp i någons dag.

Alla uppgifter delas som standard mellan hushållets medlemmar (roterande ansvar). Ett sovrum
är det enda undantaget som byggts hittills: vid "Sovrum" i våningsguiden går det att välja
vilken medlem varje enskild instans tillhör ("Delat ansvar" som standard) - den personen får
då ensamt, icke-roterande ansvar för just det rummets uppgifter. Valet erbjuds bara för
sovrum, inte andra rumstyper, och listan med vem man kan välja kommer alltid från det egna
hushållets medlemmar.

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
| Dator | Vänster sidopanel: Min dag, Områden, Planering, Hushåll, Inställningar. Användarkort längst ned |
| Platta | Sidopanel eller topprad beroende på bredd |
| Mobil | Bottenrad: Min dag, Områden, Planering, Mer |

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
