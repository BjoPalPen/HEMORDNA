# Hemordna – Produkt

Status: levande dokument. Beskriver vad produkten ska vara, inte vad som är byggt.
Implementationsstatus finns i [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 1. Produktvision

Hemordna är en svensk PWA för hushållsplanering.

> Hemordna ska hjälpa en eller flera personer i ett hushåll att veta vad som behöver göras,
> vem som ansvarar och vad som är rimligt att göra idag.

Produkten ska minska mental belastning – inte digitalisera den. Det betyder att en lyckad
användning ofta är *kortare* tid i appen, inte längre.

---

## 2. Målgrupper

| Hushållstyp | Vad Hemordna löser |
|---|---|
| Singelhushåll | Håller ordning på vad som behöver göras utan att allt ligger i huvudet |
| Par | Gör ansvar uttalat i stället för underförstått |
| Familjer | Fördelar arbete och gör varje persons del synlig och begriplig |
| Andra flerpersonshushåll | Samma sak utan antaganden om relation eller roller |

Appen antar **inte** att arbete måste fördelas lika. Ett hushåll där en person gör mer ska
fungera lika bra som ett där arbetet delas jämnt. Hemordna beskriver vad hushållet kommit
överens om – den föreskriver inte en fördelning.

---

## 3. Kärnprincip

```text
tydligt ansvar  +  tillgänglig tid  =  rimlig dagsplanering
```

Hushållet bestämmer tillsammans:

- vad som behöver göras
- hur ofta
- vem som ansvarar
- om ansvaret roterar
- när uppgiften helst ska göras

Varje individ ser sedan bara:

- vad som gäller för dem
- vad som är viktigt idag
- hur mycket tid det beräknas ta

---

## 4. Min dag är huvudskärmen

Användaren möts normalt **inte** av hela hushållets backlogg. Primärvyn är den egna dagen.

```text
Fredag

30 minuter planerade

[ ] Hall                  7 min
[ ] Dammsug vardagsrum   10 min
[ ] Matrum                5 min
[ ] Hundhår i soffan      3 min

25 av 30 minuter planerade
```

Vyn ska tillåta att:

- markera en uppgift som klar
- skjuta upp en uppgift
- ange att man har mindre tid idag
- se en glimt av morgondagen, och föra fram en enskild uppgift därifrån till idag
- ta ledigt en dag, så inget nytt landar på den

Hela hushållets backlogg ska finnas – men på en egen plats, inte som startskärm.

En uppgift som blir liggande försenad stannar hos den som redan har den. Att jämna ut arbetet
mellan hushållets medlemmar får aldrig innebära att ett redan försenat ansvar byter ägare - det
skulle bara flytta problemet, inte lösa det.

---

## 5. Tidsbudget

Varje hushållsmedlem har en normal tidsbudget per veckodag:

```text
Måndag      30 min
Tisdag      45 min
Onsdag      20 min
Torsdag     30 min
Fredag      20 min
Lördag      60 min
Söndag       0 min
```

Tidsbudgeten är **inte** metadata. Den är den huvudsakliga inputen till planeringen: det är
den som avgör vad som faktiskt hamnar på dagen.

En användare ska dessutom kunna säga *mindre tid idag* eller *ingen tid idag* utan att den
normala veckobudgeten förstörs. Undantaget gäller en dag; normen ligger kvar.

Att göra något i förväg - en uppgift som egentligen inte var ens tur än - ska märkas, inte
försvinna spårlöst i statistiken. Den som gjort mer än sin tur får färre nya uppgifter tills det
jämnar ut sig, synligt som en enda siffra ("tid i förväg") bara för dem själva - aldrig en
belastning för någon annan, aldrig ett mål att jaga, aldrig negativ.

---

## 6. Synkning

Servern är source of truth.

Markerar en person en uppgift som klar ska andra anslutna klienter i hushållet se det utan
manuell omladdning. Två personer som är hemma samtidigt ska inte behöva fråga varandra vad
som är gjort.

Offline-stöd byggs stegvis. Arkitekturen förutsätter aldrig att data bara finns lokalt i
webbläsaren.

---

## 7. Individuell presentation

Hur uppgifter visas är en **individuell** preferens, inte en hushållsinställning. Två
personer i samma hushåll kan behöva helt olika gränssnitt till samma data.

MVP-prioritet:

1. text
2. bild + text
3. stor text
4. en uppgift åt gången

Senare: endast bild, uppläsning.

**Ett presentationsval beskrivs alltid av vad det GÖR** ("kompakt lista", "en uppgift åt
gången", "en vänlig kommentar då och då"), **aldrig av vem det är för.** Inget namn, ingen
etikett, ingen förklarande text i UI:t hänvisar till en diagnos, en funktionsnedsättning eller
"tillgänglighet" - inte ens indirekt genom att antyda en målgrupp. Detta gäller varje nytt val
en person kan göra om hur de vill se sina uppgifter, oavsett hur många sådana val som läggs
till över tid.

**Ett hushåll förutsätts vara blandat** - grundfallet, inte ett specialfall. Olika medlemmar i
samma hushåll väljer normalt olika presentationslägen, olika motivationsnivå och olika
enhetsinställningar (tema, lugnare skärm) samtidigt, utan att någon av dem behöver veta vad de
andra valt. Delade ytor (hushållsöversikten, veckovyn, rumsvyn) visar aldrig vilket läge en
enskild medlem har valt - se ARCHITECTURE.md, "Beslut: Ångra och stabil lista", Del C, för den
tekniska invarianten (per medlem eller per enhet, aldrig ett hushållsgemensamt fält) det här
kravet vilar på.

---

## 8. Motivation utan skuldbeläggning

Hemordna använder inte skuldbeläggande språk och jämför inte hushållsmedlemmar med varandra.

Undvik: *"Du ligger efter"*, *"Du missade din streak"*, jämförelser mellan medlemmar,
negativa produktivitetsbudskap.

Tillåtet: *"Vill du fortsätta där du slutade?"*, *"En sak räcker också."*,
*"Det viktigaste är gjort."*, *"Dagens uppgifter är klara."*

Motivationsnivå är individuell. MVP har två lägen: **Ingen** och **Lugn**.

---

## 9. MVP-omfattning

### Household
Skapa hushåll. 1–n personer. Bjuda in medlem (senare). Aktiv/inaktiv medlem.

### HouseholdMember
Namn, tillhör hushåll, tidsbudget per veckodag, individuella preferenser.

### Areas / Rooms
Kök, badrum, vardagsrum, sovrum, tvättstuga, trädgård, hund – och egna områden. Ett område
behöver inte vara ett rum.

### Tasks
En uppgift kan ha: namn, beskrivning, område, uppskattad tid, normal ansvarig, prioritet,
recurrence, önskad veckodag, om den får skjutas upp, om ansvaret roterar, om den kräver
flera personer.

### Recurrence
Modellen ska kunna växa till: daily, weekly, every N weeks, monthly, every N months, given
veckodag, samt första/andra/tredje/fjärde veckodagen i månaden.

Recurrence-motorn ska **inte** överdesignas innan ett verkligt use case kräver den.

### Task state
En occurrence ska kunna: slutföras, skjutas upp, hoppas över, omfördelas, markeras som
"behövdes inte".

---

## 10. Utanför MVP

Byggs inte nu, och läggs inte till "för framtiden":

betalningar, avancerad auth, AI, avancerad kalender, shoppinglista, gamification, streaks,
avancerad statistik, dokumenthantering, social funktion, GPS, full offline conflict
resolution, avancerat adminsystem.

Poäng och streaks är dessutom aktivt oönskade i MVP – de drar mot den skuldbeläggning som
§8 utesluter.
