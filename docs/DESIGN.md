# Hemordna – Design

Visuell riktning och gränssnittsprinciper. Grunden lades om 2026-09-07 ("Ny form" – se
[ARCHITECTURE.md](ARCHITECTURE.md) för varför); mockupen från 2026-09-04 är ersatt.

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

Gustaviansk blå – den blågrå tonen från svenska 1700-talsinteriörer – är identiteten. Grönt
förekommer inte längre någonstans i appen. Saffran är den enda varma accenten och används
uteslutande för att markera "idag". Svenska namn i koden (`--kalk`, `--gustav` osv. i
`wwwroot/css/app.css`) – se `docs/handoff` eller `app.css` för de fullständiga
CSS-variabelnamnen.

| Roll | Namn | Värde | Används till |
|---|---|---|---|
| Bakgrund | Kalk | `#F5F3EE` | Sidbakgrund |
| Yta | | `#FFFFFF` | Kort och paneler |
| Yta 2 | | `#ECE9E2` | Sekundära ytor, chips, `btn-secondary` |
| Text | Sot | `#22282E` | Brödtext och rubriker |
| Text mjuk | | `#5C666E` | Sekundär text, etiketter |
| Linje | | `#DDDAD2` | Kortkanter, avdelare |
| Primär | Gustaviansk | `#4A6C8C` | Knappar, aktiv navigation, klar-bock |
| Primär stark | | `#3A5670` | Hover, tryckt läge, aktiv navigationstext |
| Primär mjuk | | `#DFE7EF` | Aktiv navigationsrad, primära chips |
| Accent | Saffran | `#E9B44C` | **Endast** "idag"-markering (veckogrid, dagens kolumn) |
| Accent mjuk | | `#FBEFD2` | Bakgrund för "idag"-markering |
| Sekundär | Aska | `#9AA3A8` | Fokusring, sekundära ikoner |
| Sekundär mjuk | | `#E9ECEE` | Sekundära ytor (hover) |
| Varning/ta bort | Rönn | `#C25A4A` | Ramar och ikoner för destruktiva val – **inte text**, se nedan |
| Varning/ta bort, text | | `#A04128` (`--ronn-ink`) | Text på destruktiva knappar/länkar |
| Varning/ta bort, mjuk | | `#F6E1DD` | Bakgrund för destruktiva knappar |
| Logotyp | Skifferblå | `#3F6191` | Endast logotypen (oförändrad, se §9) |

En försenad uppgift är **inte** ett fel – den markeras med ord ("sedan tidigare"), inte med
rönn. Rönn är förbehållet faktiska destruktiva val ("Ta bort").

Alla färgpar ska klara WCAG AA för text. `--ronn` (`#C25A4A`) klarar det **inte** som text mot
vare sig `--kalk` (3.90:1) eller `--ronn-soft` (3.45:1) – därför finns `--ronn-ink` (`#A04128`,
5.77:1 respektive 5.10:1) som den faktiska textfärgen på destruktiva knappar och länkar; `--ronn`
används bara till ramar och ikoner. Uppmätta värden för de tre par som är explicit
verifieringskrav: `--gustav` på `--kalk` 4.96:1, vit på `--gustav` 5.50:1, `--sot-soft` på
`--kalk` 5.29:1.

Mörkt läge är **ritat, inte inverterat** och planerat till ett senare steg (dark-tokens,
`data-theme`-växel i Inställningar) – se ARCHITECTURE.md "Ny form".

## 3. Form

- Radie: `--radius` (14px) på kort, `--radius-sm` (10px) på knappar och fält (utom piller),
  `--pill` (999px) på knappar, chips och avatarer.
- Skugga: mycket subtil, `0 1px 2px rgba(34,40,46,.06)`. Djup skapas med ramar och luft.
- Avstånd bygger på en 4px-skala: 4, 8, 12, 16, 24, 32, 48 (`--space-1`…`--space-7`,
  oförändrade).

## 4. Typografi

Två självhostade typsnitt i `wwwroot/fonts/` (SIL OFL, licensfiler bredvid), förcachade av
service workern för offline-start – ingen extern font-CDN.

| Roll | Typsnitt | Används till |
|---|---|---|
| Rubriker, navigation, knappar, chips, etiketter | Familjen Grotesk (400/500/600/700) | `--font-display` |
| Brödtext, uppgiftsbeskrivningar, hela Stor text-läget | Atkinson Hyperlegible (400/700) | `--font-body` |

| Roll | Storlek | Vikt |
|---|---|---|
| Sidrubrik ("Hej Anna!") | 28–32px | 700, Familjen Grotesk |
| Sektionsrubrik | 18px | 600, Familjen Grotesk |
| Brödtext | 15px | 400, Atkinson Hyperlegible |
| Sekundär | 13px | 400, Atkinson Hyperlegible |

Stor text-läget (se §7) skalar upp bastexten – det är inte en egen typografi. Atkinson
Hyperlegible är i sig format för läsare med nedsatt syn, så Stor text-läget får ett typsnitt
byggt för det snarare än bara en större siffra.

## 4a. Ikoner och komponenter

Inline-SVG (stroke 1.9, round caps) via `Components/Icon.razor` – inga Unicode-tecken som
ikoner. Namngivna ikoner: `sun`, `grid`, `calendar`, `people`, `chevron-right`, `plus`, `check`.

`Components/BottomSheet.razor`: ark från botten på mobil (scrim, drag-handtag, stängs med Esc,
scrim-tryck eller "Stäng", fokus flyttas in vid öppning och tillbaka vid stängning), centrerad
dialog på skärmar ≥ 640px. Används av formulär/valmenyer som byggs i senare steg.

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

### Idag (huvudvy)

Startskärmen (`MinDag.razor`, route `/`). Visar **bara den egna dagen** – aldrig hushållets
backlogg.

```text
MÅNDAG 7 SEPTEMBER
God morgon, Björn
3 av 6 klara
▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬  (tunn linje, gustav på surface-2)

KÖK                                            2 KVAR
( ) Diska eller töm diskmaskinen  [Kök]           ›
( ) Torka av bänkarna             [Kök]           ›

[Extra uppgift]

KLART IDAG                                         3
(✓) Bädda sängen                  [Sovrum]
```

Datum som versal etikett, hälsning som rubrik – tidpunkten styr ordet (God morgon/Hej/God
kväll), aldrig en emoji. Ingen tid visas – varken per uppgift eller som summa, och inget val om
tid över huvud taget (se §6a) – bara "N av M klara" och en tunn framstegslinje. Uppgifter
grupperas per rum (`RoomGroups`, oförändrad sorteringslogik) med rumsnamnet i versaler och
antal kvar till höger; en förfallen uppgift hamnar alltid först i en egen "Sedan tidigare"-grupp,
oavsett rum. "Klart idag" är en egen grupp längst ner, dämpad (55% opacitet) och genomstruken.

Varje rad (`Components/TaskListItem.razor`): en 44×44px rund bock (gustav-kant, ofylld; fylld
gustav med vit bock när klar), namn i Familjen Grotesk 600, en chevron till höger som fäller ut
beskrivning och "Skjut upp till imorgon". Svep höger på raden = markera klar (kort
gustav-soft-bekräftelse och `navigator.vibrate(10)` på mobil), svep vänster = flytta till
imorgon – bock- och skjut-upp-knapparna finns alltid kvar som vanliga knappar för tangentbord
och skärmläsare, och ett svep som börjar på en knapp gör ingenting (annars skulle det stjäla
klicket). Under `prefers-reduced-motion`: ingen dragrörelse, ingen bekräftelseflash, ingen
haptik – bara den vanliga klick-hanteringen.

De två gamla ▶-utfällningarna ("N till en annan dag", "Lägg till en extra uppgift") är nu chips
under listan ("Flytta till en annan dag", "Extra uppgift") som öppnar `BottomSheet.razor` –
samma innehåll som förut, bara i ett ark i stället för en disclosure. "Tjuvkika på ett schema"
har flyttat till Vecka (se nedan).

Tomt läge: "Ledigt idag" som en stor, lugn rubrik i stället för en blå informationsruta, med
"Nästa: onsdag, 2 uppgifter" som underrad när något är planerat inom en vecka framåt (annars
ingen underrad) – "Klart idag" ligger kvar under om något redan är gjort. Är dagens riktiga
uppgifter klara men inget mer väntar: "Dagens uppgifter är klara." i samma stil. Ingen sidopanel
längre – innehållet är alltid en enda kolumn, centrerad under den smala railen (se §8).

**En uppgift åt gången** (§7) ersätter hela listan med ett enda kort: nästa uppgift i samma
ordning listan redan skulle visat den, med **Bocka av** och **Skjut upp till imorgon** som stora
knappar. Att bocka av eller skjuta upp laddar om dagen, så nästa uppgift dyker upp av sig
själv – inget separat index att hålla reda på.

**Kända begränsningar (dokumenterade, inte lösta i detta steg):** meta-raden under namnet
visar bara "sedan tidigare" när en uppgift är försenad, inte hur ofta den återkommer –
`PlannedTaskResponse` (Api-kontraktet) bär ingen sådan text, och ett nytt fält är utanför
scope för klient-bara arbete (se CLAUDE.md, "Behöver du ett nytt API-fält: stanna och
rapportera"). "Stor text" och "En uppgift åt gången" var sparbara sedan tidigare men lästes
aldrig av `MinDag.razor` – se "Beslut: Ny form" i ARCHITECTURE.md för vad som nu är kopplat in.

### Uppgiftsdetalj

Bild, namn, områdeschip, återkommande, beskrivning, ansvarig, växlarna *Kan skjutas
upp* och *Kräver flera personer*. Primär knapp **Markera som klar**, sekundär **Skjut upp**.
Ingen tid visas; uppskattad tid sätts som ett kvalitativt läge (se §6a) och lever bara i
domänen.

### Vecka (`Vecka.razor`, döpt om från `Planering.razor`, route `/vecka`)

Hushållets veckogrid är hjälte överst: en prickmatris, en rad per medlem, en kolumn per
veckodag (idag markerad), samma matris som tidigare bara levde på Hushållsöversikten.
Sträckt ner till hela hushållet flyttar sidan fokus från "min egen vecka" till "hur ser
veckan ut för oss" utan att blanda in någon minutsiffra eller jämförelse mellan medlemmar
(PRODUCT.md §8) - bara prickar för klart/planerat/inget planerat.

Under, som ett eget `<h2>Min vecka</h2>`-avsnitt: sju rader, en per veckodag, med bara ett
kvalitativt läge i text (t.ex. "Ingen tid", "Lagom tid") – inget stapeldiagram, inga minuter,
och ingen redigering här. Helt läsläge; rollen (se §6b, satt från Hushålls `MemberSheet`) är
enda sättet att ändra veckan.

"Tjuvkika på ett schema" (en titt på i morgon, eller på någon annans dag, skrivskyddat) bor nu
här i stället för på Idag – samma disclosure och logik, oförändrad, bara flyttad. "Ser
fördelningen skev ut?" (sprid om återkommande uppgifter över veckan, `RebalanceSchedule`) bor
nu här också - flyttad hit från Rum, i ett eget ark.

### Hushållsöversikt

En avatarrad överst - en knapp per aktiv medlem (initial, namn, rolletikett), samt en sista
"Bjud in"-knapp med ett plus-ikon i stället för en initial. Att trycka på en medlem öppnar
`Components/MemberSheet.razor`: rollval (se §6b), en disclosure "Anpassad tid i stället",
paus för just den medlemmen, och "Ta bort medlem" i rönn-ink längst ner. Ingen siffra
(använd/budget) visas någonstans i raden - bara namn och rolletikett.

Vidare, i tur och ordning: hushållets veckogrid (samma prickmatris som nu även toppar Vecka),
ett tyst "Idag i hushållet"-kort (en ring, samma mönster som "Senaste händelser" nedan -
hela hushållets andel klara uppgifter idag, aldrig per medlem, PRODUCT.md §8), "Senaste
händelser", och sist en lista med "Pausa hushållet"/"Balansera om vem som gör vad"/
"Inställningar"/"Logga ut" som listrader - de två första öppnar varsitt eget ark.

Detta är den enda vyn som visar hela hushållet, och den är aldrig startskärm - därför är den
också platsen för roll-/tidsinställningar som inte alla medlemmar behöver se eller röra vid,
till skillnad från Min dag som alla öppnar varje dag.

"Bjud in" (`BottomSheet`) slår ihop två funktioner bakom en enda ingång: hushållets
inbjudningskod (åtta tecken, versaler, inga förväxlingsbara siffror/bokstäver) med en "Dela
koden"-knapp (plattformens delningsruta där den finns, annars kopiering till urklipp) och en
knapp för att skapa en ny kod om den gamla hamnat i fel händer, samt - som en disclosure
"Eller lägg till en medlem utan eget konto" - formuläret för att lägga till någon som inte
skapar ett eget konto än (t.ex. ett barn). Koden delas fortfarande manuellt (ingen e-post/
länk ännu) - personen som bjuds in anger koden på sin egen "Skapa konto"-skärm i stället för
att döpa ett nytt hushåll.

"Balansera om vem som gör vad" (ombalansera roterande ansvar, `RebalanceTaskAssignments`) bor
i ett eget ark här, flyttad hit från Rum. "Pausa hushållet" har på samma sätt flyttat in i ett
eget ark i stället för att ligga som ett alltid synligt formulär på sidan.

Har hushållet fler än en våning (se Rum nedan) räknas det med i sidhuvudet ("N personer · M
rum · K våningar") - samma härledning som Rum använder, delad via `Support/RoomFloors.cs` så
de två sidorna inte har varsin kopia av samma " – "-parsning.

### Rum (`Rum.razor`, route `/rum`)

Hemmets rum, och all uppgiftshantering, samlat på ett ställe - det finns ingen egen
"Uppgifter"-sida (produktfeedback: kändes konstigt att hantera uppgifter någon annanstans än
i rummet de redan hör till).

Rubrik "Rum". Har hushållet fler än en våning: en segmentkontroll överst väljer vilken -
härledd från rumnamnens "Våning – "-prefix (`Rum.razor.FloorOf`), eftersom våning inte är ett
eget fält i domänen och att lägga till ett är utanför vad ett klient-bara steg får göra; ett
rum som byts namn för hand så prefixet försvinner hamnar i "Annat". Under: `RoomTile.razor` i
två kolumner (en under 360px) - namn, "N uppgifter · M min/v", och en saffran-soft "N idag"
om den inloggade medlemmen har något där idag, annars "Nästa: veckodag" i gustav-ink (kapat
vid 7 dagars sökning, delad över alla brickor i ett svep - se `LoadTodayAndNextAsync`). Ett
"Övrigt"-rum utan riktigt `Area` samlar rumslösa uppgifter, alltid synligt. Sist en streckad
"+ Nytt rum"-bricka.

Att trycka på en bricka öppnar `Components/RoomSheet.razor`: rummets uppgifter som en enkel
lista (namn, upprepning, minuter, "roterar"/medlemsnamn, "endast vuxna"), en "Rummets meny"
(⋯) med "Ändra frekvens för hela rummet"/"Byt namn"/"Ta bort rum", och en "+ Lägg till
uppgift"-rad. Att trycka på en uppgift öppnar `Components/TaskOptionsSheet.razor`:
Upprepning/Vem gör det/Rum som varsin rad som drillar ner till ett eget litet formulär inuti
samma ark, en "Kräver vuxen"-växel, och "Ta bort uppgiften" i rönn-ink längst ner. "+ Nytt
rum" öppnar ett ark med rumsmalls-väljaren (namnge våning, rumstyp, antal - se §6b) och en
disclosure "Lägg till ett tomt rum i stället" för grupperingar som inte är ett rum (t.ex.
"Hund", "Garage").

Totalrad "Totalt: N uppgifter · M min" (en platt summa, till skillnad från varje bricka
egen viktade "min/v") behålls som dämpad text under brickorna, tillsammans med den
frekvensviktade veckosumman och hushållets samlade veckokapacitet - se "Beslut:
`TaskWorkload`" i ARCHITECTURE.md.

"Känns det som att en person gör för mycket?" (ombalansera ansvar) flyttade till Hushåll;
"Ser fördelningen skev ut?" (sprid om schemat) flyttade till Vecka - se docs/ARCHITECTURE.md
"Ny form".

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

Undantaget gäller uttryckligen bara den dagliga vyn (Idag). Rum-skärmens `RoomTile`/
`RoomSheet` visar minuter per rum och per uppgift ("N min/v", "M min") - samma redan
etablerade undantag som `Omraden.razor`s totalrad alltid haft: under planering av hemmet är
"hur lång tid tar det här?" en rimlig fråga att svara på med en siffra.

### 6b. Roller och rumsmallar – färre val vid start

Även fyra kvalitativa lägen per veckodag var för många beslut på en gång (produktfeedback).
`Hemordna.Client.Support.HouseholdRolePresets` erbjuder tre roller istället – **Vuxen, jobbar
heltid**, **Barn eller ungdom**, **Pensionär / hemma dagtid** – och räknar ut en rimlig
vardag/helg-fördelning åt medlemmen i ett enda val.

Rollvalet sätts vid "Lägg till medlem" och kan ändras därefter genom att öppna medlemmens egen
`MemberSheet` från avatarraden på Hushållsöversikten - inte på Min dag. Uppföljande feedback:
att visa och kunna ändra en roll är i sig "tid som ett val", och det behöver inte alla
medlemmar se eller ta ställning till varje gång de öppnar appen. Hushållsöversikten är redan en
sida ingen är tvungen att besöka dagligen, till skillnad från Min dag, så den är rätt plats för
den här typen av inställning. `HouseholdRolePresets.Match` känner igen om en medlems sparade
budget kommer från en roll eller är satt för hand (då visas "Anpassad tid" i stället).

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

Fyra flikar, samma ordning, samma innehåll på mobil och dator – ingen enhet visar fler eller
färre destinationer än någon annan:

| Flik | Route | Ikon | Ersätter |
|---|---|---|---|
| Idag | `/` | `sun` | Min dag |
| Rum | `/rum` | `grid` | Områden (`/omraden` omdirigerar hit) |
| Vecka | `/vecka` | `calendar` | Planering (`/planering` omdirigerar hit) |
| Hushåll | `/hushall` | `people` | oförändrad |

Inställningar och "Mer" är inte längre flikar – `/mer` omdirigerar till `/hushall`, och
Inställningar samt Logga ut nås som listrader längst ned på Hushåll (se `Hushall.razor`).

| Yta | Mönster |
|---|---|
| Dator (≥ 900 px) | Smal vänster rail (~72 px, ikon + etikett, inget 260 px-sidofält). Innehåll centrerat, max 640 px |
| Mobil (< 900 px) | Bottenrad med samma fyra flikar |

Idag är alltid första valet och startvyn.

---

## 9. Logotyp

Ett hus omgivet av en bladkrans, i blått. Signalerar hem och lugn, inte effektivitet.

`logo.png` ligger i `src/Hemordna.Client/wwwroot/brand/`. Används i sidhuvud, inloggningssidan
och laddningsskärmen. `icon-192.png`/`icon-512.png` (PWA-manifestet, hemskärmsikon) och
`favicon.png` (webbläsarfliken) är nedskalade kopior av samma bild, direkt i `wwwroot/`.
Byt aldrig färg på märket; placera det på ljus bakgrund.

Ordbild: **Hemordna**, med underraden *Ett enklare hem, en lugnare vardag*.

---

## 10. Tillgänglighet

- Kontrast enligt WCAG AA.
- Kryssrutor och knappar minst 44×44px träffyta.
- Färg är aldrig ensam bärare av betydelse – status har alltid text eller ikon.
- Fokusmarkering syns tydligt och tas aldrig bort.
- Stor text-läget får inte bryta layouten.
