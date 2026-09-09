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

Mörkt läge är **ritat, inte inverterat**: egna, kontrastverifierade mörka tokenvärden i
`app.css`, applicerade automatiskt via `prefers-color-scheme` eller tvingat via `data-theme`
("Utseende" i Inställningar, ett per-enhet val i `localStorage` - se ARCHITECTURE.md "Ny form"
steg 5 för de fullständiga värdena och verifieringen).

## 3. Form

- Radie: `--radius` (22px) på kort och listor, `--radius-sm` (14px) på fält, `--radius-xl`
  (26px) på det fullhöga arket (se §4a), `--pill` (999px) på knappar, chips och avatarer. Höjt
  från 14px/10px i "Ny form" (steg 1) - "Ny form 2026" mjukar upp ytorna ytterligare, se
  ARCHITECTURE.md. Samma sex ytor har också `corner-shape: squircle` (progressiv förbättring).
- Kant: `--edge` - transparent i ljust läge (en vit yta läser redan mot `--kalk` utan en ritad
  linje), `var(--line)` i mörkt läge (en mörk yta behöver en riktig kant för att skiljas från
  bakgrunden). Ersätter `var(--line)` som ytterkontur på kort, listor och rumsbrickor; radskiljare
  inuti listor behåller `var(--line)` oförändrat.
- Skugga: mycket subtil, `0 1px 0 rgba(34,40,46,.04)` i ljust läge (mörkt läge oförändrat).
  Djup skapas med ramar och luft, inte skugga.
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
dialog på skärmar ≥ 640px, `corner-shape: squircle` och `--radius-xl` (26px) på det översta
hörnparet.

**Två höjdlägen** ("Ny form 2026", `Components/SheetDetent.cs`): `Half` (56vh) för ett kort
val/en sammanfattning som inte behöver hela skärmen, `Full` (85vh, oförändrat) för ett formulär
som kan behöva växa. Desktopdialogen ignorerar detent helt - alltid 80vh. Ett drag uppåt (> 60px)
på handtaget eller rubrikraden flyttar ett `Half`-ark till `Full` för resten av den öppningen;
ett drag nedåt (> 80px) stänger arket samma väg som "Stäng" gör. Rent tillägg, aldrig enda vägen
- "Stäng"-knappen och Esc fungerar precis som förut oavsett läge. `--sheet-scrim` fick
`backdrop-filter: blur(3px)` - den tonade ytan bakom arket, inte arket självt (glas-transparens
är annars förbehållet navigationspillen, se §8).

**Fjädrande bekräftelse** ("Ny form 2026"): `task-swipe.js`s delade `confirm()` (bock-knappen
och svepet på Idag, se §6) lägger på `.task-confirm`, en `task-spring`-keyframe-animation (skala
1 → 1.025 → 1, `cubic-bezier(.2, 1.4, .4, 1)`, .32s) i stället för bara en färgövergång - en
mjuk, "studsande" bekräftelse snarare än en platt flash. `navigator.vibrate(10)` körs fortfarande
på mobil där webbläsaren stödjer det, men **iOS Safari ignorerar `navigator.vibrate` helt och
tyst** - det beskrivs därför aldrig i produkttext eller marknadsföring som en funktion appen har,
bara som ett bästa-möjliga tillägg på de plattformar (i praktiken de flesta Android-webbläsare)
som faktiskt stödjer det. Under `prefers-reduced-motion` lägger `confirm()` aldrig till klassen
alls (returnerar tidigt) - keyframes körs därför aldrig, ingen ytterligare avstängning behövs.

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

**Inga idiom, inga metaforer, inga lekfulla omskrivningar. En knapp säger vad den gör.**
"Tjuvkika på ett schema" (en gissningslek om vad "tjuvkika" innebär) blev "Se någon annans dag".
"Ser fördelningen skev ut?" (en bild, inte en fråga om vad knappen faktiskt gör) blev "Vill du
fördela om dagarna?", knappen själv "Sprid ut över veckan" (idiom - sprider man verkligen ut
något?) blev "Fördela om dagarna". Samma regel gäller retroaktivt för allt nytt språk i denna
revision - se "Beslut: Ångra och stabil lista" i ARCHITECTURE.md.

**Inga förkortningar i UI-text**: "till och med", inte "t.o.m.". Enheter ("min") är tillåtna.

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
oavsett rum. Har hushållet fler än en våning klustras rummen ytterligare ett steg
(`FloorGroups`) - en `<h2>`-våningsrubrik ("Övre plan") följt av dess egna rumsrubriker, så en
vånings rum alltid står tillsammans i stället för utspridda i `DailyPlanner`s egen,
våningsblinda ordning. Ett enplanshushåll ser ingen våningsrubrik alls. "Klart idag" är en egen
grupp längst ner, dämpad (55% opacitet) och genomstruken.

Varje rad (`Components/TaskListItem.razor`): en 44×44px rund bock (gustav-kant, ofylld; fylld
gustav med vit bock när klar), namn i Familjen Grotesk 600, en chevron till höger som fäller ut
beskrivning och "Skjut upp till imorgon". Att bocka av - via bock-knappen ELLER genom att svepa
höger på raden - ger samma korta, fjädrande bekräftelse (se §4a) och `navigator.vibrate(10)` på
mobil (`task-swipe.js`s delade `confirm()`, se ARCHITECTURE.md "Ny form" steg 5 och "Ny form
2026"); svep vänster = flytta till imorgon. Bock- och skjut-upp-knapparna finns alltid kvar som
vanliga knappar för tangentbord och skärmläsare, och ett svep som börjar på en knapp gör
ingenting (annars skulle det stjäla klicket). Under `prefers-reduced-motion`: ingen dragrörelse,
ingen bekräftelseflash, ingen haptik – bara den vanliga klick-hanteringen, oavsett om den kom
från bocken eller svepet.

**Jag börjar nu** (denna revision): en knapp i den utfällda raden (och i fokuskortet, mellan
"Bocka av" och "Skjut upp") markerar en enda uppgift som pågående - `<span class="chip
chip-primary">Pågår</span>` bredvid namnet, raden flyttas överst i sin grupp (klientsidan, bara
visning), och "Börja här" (nedan) döljs så länge något pågår. Enhetslokalt och per dag
(`Support/StartedTask.cs`, `hemordna.started` i `localStorage`) - ingen server vet om det, ingen
tid räknas, ingen timer. Rensas när uppgiften bockas av eller skjuts upp, eller tyst av sig
självt när dagen byter (en gammal markering för gårdagens datum ignoreras). "Lugn" läser en
pågående uppgift som "Vill du fortsätta där du slutade?" - samma fras som att redan ha bockat av
något ger.

**Börja här** (denna revision): planerarens egen första uppgift (samma ordning fokusläget redan
använder - "Sedan tidigare" först, annars första raden i första rummet) får en tyst
`chip-today`-chip, "Börja här", bredvid namnet - bara i listläge, bara när fler än en uppgift
väntar (annars är det redan uppenbart var man börjar) och bara så länge ingen uppgift redan är
igångsatt (se "Jag börjar nu" ovan). Chipparnas ordning under namnet: rum, tid, "Börja här".

**Skriv ut** (denna revision): en "Skriv ut"-länk under "Klart idag" (eller under chip-raden om
inget ännu är klart) öppnar webbläsarens vanliga utskriftsdialog. Sidan har en egen,
alltid uppbyggd (men på skärmen alltid dold) utskriftsvy - så en utskrift i fokusläge ändå visar
hela dagens lista, inte bara det enda kort skärmen själv visar där. Svartvitt, tom kvadrat i
stället för bock-knappen, namn och eventuella steg under, rumsrubriker håller ihop över en
sidbrytning.

**Steg i beskrivningen** (denna revision): en beskrivning skriven en rad per steg (t.ex. "Ta
fram hinken", ny rad, "Fyll med varmt vatten") renders som en numrerad lista i stället för ett
enda textstycke, i den utfällda raden och i fokuskortet (`Support/TaskSteps.cs`). En
enradsbeskrivning renderas som idag, ett vanligt stycke. En redan självnumrerad rad ("1. Ta fram
hinken") får sin egen siffra bortstädad så listan aldrig visar dubbla nummer. Beskrivningsfältet
i "Extra uppgift" har platshållartexten "En rad per steg om du vill" - ett förslag, inget krav.

De två gamla ▶-utfällningarna ("N till en annan dag", "Lägg till en extra uppgift") är nu chips
under listan ("Flytta till en annan dag", "Extra uppgift") som öppnar `BottomSheet.razor` –
samma innehåll som förut, bara i ett ark i stället för en disclosure. "Flytta till en annan dag"
rendras alltid (aldrig villkorligt gömd) - utan något att flytta blir den `aria-disabled` och
svarar med en statusrad i stället för att öppna ett tomt ark (se "Beslut: Ångra och stabil
lista" §B7 i ARCHITECTURE.md). "Se någon annans dag" har flyttat till Vecka (se nedan).

"Extra uppgift" visar i första hand en lista av hushållets befintliga uppgifter som inte redan
är på dagens lista (namn, kvalitativt tidsläge) - grupperad per rum/våning precis som Idags
egen lista (samma "hålla ihop"-mönster, se §6 nedan), inte en enda platt lista, eftersom den
annars blev lång och svårbläddrad. En tryckning schemalägger den direkt för i dag, ingen ny
uppgift skapas och inget behöver skrivas in på nytt. "Eller skriv en ny uppgift" är en
disclosure under listan med det gamla formuläret (namn, tidsnivå och - om hushållet har rum -
ett rumsval som förvalt "Övrigt") för en genuint ny, engångssak; rumsvalet avgör var uppgiften
hamnar på Idag precis som för alla andra uppgifter. Har hushållet inga befintliga uppgifter att
erbjuda visas formuläret direkt i stället för en tom, meningslös lista.

Tomt läge: "Ledigt idag" som en stor, lugn rubrik i stället för en blå informationsruta, med
"Nästa: onsdag, 2 uppgifter" som underrad när något är planerat inom en vecka framåt (annars
ingen underrad) – "Klart idag" ligger kvar under om något redan är gjort. Är dagens riktiga
uppgifter klara men inget mer väntar: "Dagens uppgifter är klara." i samma stil. Ingen sidopanel
längre – innehållet är alltid en enda kolumn, centrerad under den smala railen (se §8).

**En uppgift åt gången** (§7) ersätter hela listan med ett enda kort: nästa uppgift i samma
ordning listan redan skulle visat den, med **Bocka av**, **Skjut upp till imorgon** (om
uppgiften får skjutas upp) och **Visa nästa** (en tredje, länk-stilad knapp, dold när bara en
uppgift återstår) som roterar till nästa i samma ordning utan att röra servern eller Vecka - en
titt, inte en handling. Att bocka av eller skjuta upp laddar om dagen, så nästa uppgift dyker
upp av sig själv – inget separat index att hålla reda på (se "Beslut: Ångra och stabil lista"
§B4 i ARCHITECTURE.md).

**Ångra** (§B1): en avbockning ger 8 sekunder att ta tillbaka den - en rad, `role="status"`,
direkt under headern (eller under fokuskortet i "En uppgift åt gången"): "Klar: {namn}" och en
"Ångra"-länk. Bara den som bockade av kan ångra, bara inom 15 minuter (domänregel, inte en
UI-gräns) - se `TaskOccurrence.Reopen` i ARCHITECTURE.md.

**En realtidsändring ritar aldrig om listan mitt i en interaktion** (§B2): en annan medlems
egen handling patchar `_day` på plats i stället för att ladda om allt - en rad som blev klar
hos någon annan stannar kvar där den var (dämpad, "Klar: {namn}"), en ny rad läggs sist i sin
rumsgrupp, inget byter ordning. En kort informationsrad, `<p class="remote-note"
role="status">`, visar "Helena bockade av Diska." (det riktiga namnet när medlemmen fortfarande
finns i hushållet, annars "Någon annan bockade av Diska."; "N uppgifter blev klara av andra."
vid flera) i 6 sekunder - ren information, aldrig en jämförelse mellan medlemmar (se Del C
nedan: aldrig ett antal per person, aldrig en ordning mellan personer). Uppskjuts 2s i taget om
ett ark är öppet eller medlemmen just interagerat.

**Tak på "Sedan tidigare"** (§B6): fler än fem försenade uppgifter visar de tre KRONOLOGISKT
äldsta (`OriginalScheduledDate`, namn som tiebreak), följt av en rad "… och N till" med två
länkar - "Visa alla" (lokalt, ingen server-ändring) och "Låt Hemordna sprida ut dem" (kör samma
ombalansering som Vecka har, laddar om dagen, visar "N uppgifter fördelades på andra dagar.").
Rubrikens eget "N kvar" räknar alltid hela listan, capad eller inte.

**"Imorgon" och att jobba i förväg** (denna revision): ett hopfällt `<details>`-avsnitt längst
ner - "Imorgon" plus antal uppgifter i sammanfattningen, öppnat visar antingen "Inget planerat
imorgon än." eller en enkel, skrivskyddad lista med varje rads egen "Gör idag i stället"-länk.
En rad som förs fram flyttar sig omedelbart till Idags egen lista med en tyst "I
förväg"-chip (samma stil som tidsnivå-chippen, aldrig en varning) och försvinner ur "Imorgon" -
listan filtrerar uttryckligen bort allt som redan finns på dagens egen lista, annars skulle
`DailyPlanner`s "kvarstående och senast denna dag"-regel visa samma rad på båda ställena. En egen
chip i `.chips`-raden, "Ta ledigt idag" (blir "Ledig idag" när dagen redan är markerad), öppnar
`Components/DayOffSheet.razor` - ett val mellan att ta med redan planerat till idag eller skjuta
upp det till nästa lediga dag, och en banderoll "Du är ledig idag. Inget nytt läggs på dig." när
dagen är markerad. Samma ark, samma val, nås även från "Imorgon" för morgondagens datum.

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
(PRODUCT.md §8) - bara prickar för klart/planerat/inget planerat, plus (denna revision) en
streckad ring - `dot-off` - för en dag medlemmen själv markerat ledig. En ledig dag är avsiktligt
lika synlig för hela hushållet som klart/planerat: neutral planeringsinformation, inte en privat
uppgift (se ARCHITECTURE.md Del C/G).

Under, som ett eget `<h2>Min vecka</h2>`-avsnitt: sju rader, en per veckodag, med bara ett
kvalitativt läge i text (t.ex. "Ingen tid", "Lagom tid") – inget stapeldiagram, inga minuter,
och ingen redigering här. Helt läsläge; rollen (se §6b, satt från Hushålls `MemberSheet`) är
enda sättet att ändra veckan.

Direkt under rubriken, bara när den inloggade medlemmen faktiskt har någon: "Tid i förväg: N
min" (denna revision, `GetMemberTimeCredit`) - ett tal, en mening, ingenting mer (se §6a). Aldrig
"0 min" när saldot är noll - raden utelämnas helt i stället, så den aldrig läses som ett mål att
nå. Bara den inloggade medlemmens egen balans - ingen annan medlems siffra visas någonstans, till
skillnad från prickmatrisens `dot-off` ovan (se ARCHITECTURE.md Del C/G för varför de två skiljer
sig åt).

"Se någon annans dag" (en titt på i morgon, eller på någon annans dag, skrivskyddat) bor nu
här i stället för på Idag – samma disclosure och logik, flyttad. Listan visar en kryssruta
(ifylld för avklarat, tom annars), uppgiftens namn och dess rumschip (samma `.chip`-mönster
`TaskListItem.razor` använder - utan den går två likadant namngivna uppgifter i olika rum,
t.ex. "Vädra rummet" i två sovrum, inte att skilja åt; en produktionsrapporterad förvirring,
fixad); "sedan tidigare" behålls dock på en utestående uppgift, annars ser en dags gamla, ej
avklarade uppgift ut som en rak dubblett av morgondagens egna nya förekomst (se
`PeekScheduleTests`, en tidigare rapporterad förvirring).
Under listan: "Totalt: N min" (`DailyPlanResponse.PlannedMinutes + CompletedMinutes`) - ett
uttryckligt, medvetet undantag från §6a på produktfeedback: att se en annan dag är att
bedöma hur full den är, närmare planeringsläget Rum/RoomTile redan har ett minutundantag för
än den egna dagliga vyn. "Vill du fördela om dagarna?" (sprid om återkommande uppgifter över
veckan, `RebalanceSchedule`, knappen "Fördela om dagarna") bor nu här också - flyttad hit från
Rum, i ett eget ark.

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
Upprepning/Vem gör det/Tid/Rum som varsin rad som drillar ner till ett eget litet formulär inuti
samma ark ("Tid" med samma kvalitativa knappar - Ingen/Lite/Lagom/Lång tid, se §6a - som
"Lägg till uppgift" redan använder), en "Kräver vuxen"-växel, och "Ta bort uppgiften" i rönn-ink längst
ner. "+ Nytt rum" öppnar ett ark med rumsmalls-väljaren (namnge våning, rumstyp, antal - se §6b) och en
disclosure "Lägg till ett tomt rum i stället" för grupperingar som inte är ett rum (t.ex.
"Hund", "Garage").

Totalrad "Totalt: N uppgifter · M min" (en platt summa, till skillnad från varje bricka
egen viktade "min/v") ligger tillsammans med den frekvensviktade veckosumman och hushållets
samlade veckokapacitet bakom en disclosure `<summary>Visa tid</summary>` under brickorna - se
"Beslut: Ångra och stabil lista" §B5 i ARCHITECTURE.md; siffrorna själva är oförändrade, bara
frivilliga att öppna i stället för alltid synliga (se "Beslut: `TaskWorkload`" i
ARCHITECTURE.md för hur de räknas ut).

"Känns det som att en person gör för mycket?" (ombalansera ansvar) flyttade till Hushåll;
"Vill du fördela om dagarna?" (sprid om schemat) flyttade till Vecka - se docs/ARCHITECTURE.md
"Ny form".

### 6a. Tid hanteras i bakgrunden, visas aldrig

Domänen räknar fortfarande i minuter (uppskattad tid, veckobudget, `availableMinutes` från
API:t) – det är vad `RecurrenceRule`, `DailyPlanner` och rotationslogiken behöver för att
räkna ut vad som får plats en given dag. Klienten mappar minuter till fyra kvalitativa lägen
(`Hemordna.Client.Support.TimeLevel`: Ingen tid/Lite tid/Lagom tid/Lång tid → 0/5/15/30 min -
"Ingen tid" är ett giltigt, sparbart val, inte bara ett tomt förval).

**Nivåorden används när man VÄLJER; minuter visas där tid VISAS, efter eget val.**
Nivåorden är bra som val (fyra alternativ att jämföra) men vaga som information - "Lite tid"
säger inte om det är 5 eller 12 minuter, och en 45-minutersuppgift läses som "Lång tid" precis
som en 30-minuters. En rad som visar en redan sparad tid (`TaskListItem`s `.chip-time`,
fokuskortets egen chip) står därför alltid som minuter rakt av - "5 min" - via
`TimeLevel.MinutesLabel`, aldrig avrundat till närmaste nivå och aldrig `TimeLevel.LabelFor`s
nivåord. `ShowTimeLevel` styr fortfarande OM tiden visas alls (av som standard - se nedan);
detta gäller bara VAD den visar som när den är på. Inga förkortningar eller hedge-ord ("ca",
"ungefär") på raderna - att tiderna är uppskattningar sägs en gång, i Inställningar vid valet,
inte upprepat varje gång en tid visas. Nivåordens knappar (`.level-picker` - "Extra uppgift",
`TaskOptionsSheet`, `RoomSheet`, medlemsformulären) visar sitt eget ord OCH minuterna under, så
valet aldrig är en gissning om vad ett nivåord som "Lite tid" faktiskt sparas som.

Bakgrund: alltför mycket tidsvisning (minuträknare, progress-ringar, stapeldiagram) skapar
stress snarare än lugn – motsatsen till appens syfte. Uppgiften och bocken räcker; tiden är
ett internt planeringsverktyg, inte något användaren ska behöva förhålla sig till. Det gäller
även ett litet, kvalitativt val: dagens tillfälliga avvikelse (`availableMinutes`) går
fortfarande att sätta via API:t, men har ingen knapp någonstans i gränssnittet längre - även
det visade sig kännas som "tid som ett val".

Undantaget gäller uttryckligen bara den dagliga vyn (Idag). Rum-skärmens `RoomTile`/
`RoomSheet` visar minuter per rum och per uppgift ("N min/v", "M min") - samma redan
etablerade undantag som `Omraden.razor`s totalrad alltid haft: under planering av hemmet är
"hur lång tid tar det här?" en rimlig fråga att svara på med en siffra. "Tjuvkika på ett
schema" (§6, Vecka) har samma undantag för sin egen "Totalt: N min"-rad under den tjuvkikade
dagens lista - att bedöma en dags omfång är planering, inte den dagliga vyn själv. Vecka har
ytterligare ett, snävare undantag (denna revision): "Tid i förväg: N min" under "Min vecka" -
avsiktligt bara ETT tal och EN mening, aldrig ett diagram, en historik eller en streak (CLAUDE.md
§12) - se ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg".

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

Inställningar sparas när de ändras. Ingen Spara-knapp, ingen blandning av direkt och
uppskjutet. Undantag: lösenordsbyte.

Ett eget "Utseende"-kort (ljust/mörkt/systemets eget) sitter direkt under - se §2 för
tokenvärdena. Till skillnad från "Min visning" ovanför sparas valet inte mot servern
(`MemberPreference`), utan i `localStorage` och appliceras direkt vid val: rätt tema hör till
enheten, inte till personen, så det ska inte följa med till någon annan skärm de loggar in på.

---

## 7. Presentationslägen

Individuell preferens, aldrig en hushållsinställning. Beskrivs alltid av vad ett läge GÖR
("kompakt lista", "en uppgift åt gången"), aldrig av vem det är för - se docs/PRODUCT.md §7.

| Läge | Status |
|---|---|
| Text (standard) | MVP |
| Bild + text | MVP |
| Stor text | MVP |
| Bild + stor text | MVP |
| En uppgift åt gången | MVP |
| En uppgift åt gången med bild + stor text | MVP |
| Endast bild | Senare |
| Uppläsning | Senare |

Lägena ska byta *presentation* av samma data – inte vilken data som visas.

**Bild + stor text, och samma kombination i fokusläge**: `PresentationMode` bär tre
saker - bild, stor text, en-i-taget-läge - som fasta, namngivna kombinationer snarare än tre
oberoende växlar (se `Client/Support/PresentationModes.cs`, den enda platsen som vet vilket
namngivet läge som slår på vilka fakta). `ImageAndLargeText` visar samma grupperade lista som
"Bild + text" men med stor text påslagen också; `OneAtATimeImageAndLargeText` är fokuskortet
("En uppgift åt gången") med både ikon och stor text. Lagras som enumens ordinaltal
(`integer`-kolumn, inte en sträng) - nya lägen läggs alltid till sist, aldrig in mellan
befintliga, annars byter en redan sparad rad tyst mening.

**Motivation** (`Installningar.razor`s eget `<h2>Motivation</h2>`-kort, `MotivationLevel {
None, Calm }`) är nu på riktigt kopplad in: "Lugn" visar en av tre fasta fraser
(`day-encouragement`, direkt under "N av M klara" på Idag) valda helt av dagens tillstånd -
"Det viktigaste är gjort." (allt eller minst hälften klart), "En sak i taget räcker." (fler än
fyra kvar), annars "Här är dina uppgifter för idag." Aldrig slumpmässigt, aldrig en jämförelse
mellan medlemmar (§5) - se "Beslut: Ångra och stabil lista" §B3 i ARCHITECTURE.md.

**Visa tid** (`ShowTimeLevel`, en egen växel i "Fler val" under presentationsvalen) byter
`TaskListItem`s tidsangivelse mellan osynlig och en kvalitativ nivå-chip ("Lite tid"/"Lagom
tid"/"Lång tid") - Idag visar aldrig en minutsiffra, oavsett läge (se §6a). Samma chip i
fokuskortet när läget är "En uppgift åt gången". Se §B5 i ARCHITECTURE.md.

**Snabbval** (tre chips - Kompakt, Tydlig, Steg för steg - överst i presentationskortet, §B11)
fyller i presentation, motivation, visa tid och lugnare skärm på en gång, men beskrivs bara av
vad de gör: "Kompakt" (Text, ingen motivation, ingen tid, ingen lugnare skärm), "Tydlig" (bild
och text, ingen motivation, tid PÅ, ingen lugnare skärm), "Steg för steg" (en uppgift åt
gången, Lugn, tid PÅ, lugnare skärm PÅ). Presentation, motivation och tid sparas först vid
"Spara"; lugnare skärm (nästa stycke) gäller omedelbart, preset eller inte.

**Lugnare skärm** (`CalmScreen`, `hemordna.calm` i `localStorage`, `data-calm` på `<html>`) är
enhetslokal, precis som temat - inte en `MemberPreference`. Stänger av alla transitions/
animationer app-brett (samma regler som `prefers-reduced-motion`), gör nav-piller och
fade-kanter solida i stället för genomskinliga, och hoppar över svep-dragrörelsen. Se §B8 i
ARCHITECTURE.md.

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
| Mobil (< 900 px) | Svävande pill, fristående från kanterna ("Ny form 2026", se nedan) |

Idag är alltid första valet och startvyn.

**Mobil: svävande pill i stället för en fast bottenrad** ("Ny form 2026" - se ARCHITECTURE.md).
Piller flyter `max(14px, env(safe-area-inset-bottom))` från underkanten, centrerad, med en
tonad `--glass`-bakgrund (`backdrop-filter: blur(18px) saturate(1.3)`) och `--shadow-float` -
det enda stället i appen glas-transparens används, eftersom det bär navigation, inte innehåll
(se ARCHITECTURE.md, "vad som medvetet inte görs"). **Alla fyra namn syns alltid** - ingen
flik döljer sin text, varken vilande eller efter scroll (se "Beslut: Ångra och stabil lista"
§B9 i ARCHITECTURE.md: en flik som ibland bara är en ikon och ibland har text är precis den
sortens oförutsägbarhet uppdraget tar bort). Har sidan scrollats (`html[data-scrolled]`, satt
av `Support/ScrollState.cs`) krymper piller till ett kompaktare läge (mindre `font-size` och
padding på `.nav-label`) för att orden fortfarande ska få plats, snarare än att gömma dem.
`.app-main`s bottenmarginal är 112px på mobil så sista raden i en lång lista alltid scrollar
helt fri från pillens egen ruta.

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
