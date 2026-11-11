# STORE-SPIKE-01 – Resultat

Datum: 2026-10-20 · Branch: `spike/store-spike-01-willys` · Uppdrag:
[../STORE-SPIKE-01-Willys-Agentuppdrag.md](../STORE-SPIKE-01-Willys-Agentuppdrag.md) ·
Beslut: [decision.md](decision.md)

Faktaetiketter enligt uppdraget §8: `VERIFIED` betyder läst eller kört i spiken med bevis i
`evidence/`. `OBSERVED` betyder sett i källkod eller dokumentation men inte provat. `ASSUMED`
betyder att det bara står i tredjepartskod. `NOT VERIFIED` betyder att det inte har kunnat
undersökas.

## Viktigt om metoden: inga live-anrop mot Willys

Uppdraget tillät live-sökning om villkoren medgav det. **Ingen sådan sökning har gjorts, och
ingen klient för Willys har byggts.** Skälet:

- `robots.txt` stänger `/sok` (söksidan) och `/varukorg` för automatiserade besökare. Beviset
  finns i [evidence/robots-txt-2026-10-20.txt](evidence/robots-txt-2026-10-20.txt).
- Open source-klienterna söker via `/search/clean` och ändrar varukorgen via
  `/axfood/rest/cart`. Ingen av de adresserna står i `robots.txt`.
- Att skicka samma sökning via en adress som inte står i listan vore att gå runt Willys
  uttalade vilja genom en annan dörr. Det bryter mot uppdragets regel om att inte kringgå
  åtkomstskydd (§6). Björn bekräftade den tolkningen under arbetet.

Följden är att allt som sägs om Willys API nedan bygger på källkod och dokumentation, inte på
observerade svar. Det är en medveten begränsning. Huvudfrågan avgörs ändå inte av hur API:t
beter sig, utan av vad som är tillåtet och lämpligt (se [decision.md](decision.md)).

## Svar per fråga (uppdraget §2)

### 1. Produktsökning

- **OBSERVED:** båda granskade klienterna söker med `GET /search/clean?q=<text>&size=<n>&page=<n>`
  mot `https://www.willys.se`, utan inloggning.
- **ASSUMED:** svaret är JSON med `results[]` och `pagination`. Det kommer från `types.ts` i
  willys-agent och `search.go` i willys-mcp.
- **VERIFIED:** `robots.txt` har `Disallow: /sok`. Automatiserad sökning är alltså inte
  välkommen.
- **NOT VERIFIED:** att anropet fungerar i dag. Det har inte provats, med avsikt.

### 2. Produktidentifiering

- **ASSUMED:** produktnyckeln är `code` med en enhetssuffix, t.ex. `101175556_ST` (styck) och
  troligen `_KG` (vikt). Varukorgen tar emot samma kod som `productCodePost`.
- **NOT VERIFIED:** om `code` är stabil över tid och mellan butiker. Inget av projekten läser
  EAN/GTIN, och det är okänt om svaret innehåller det.

### 3. Priser

- **ASSUMED:** svaret innehåller `priceValue` (tal), `price` (text), `priceUnit`,
  `comparePrice` och `comparePriceUnit` (jämförpris), `savingsAmount` och
  `potentialPromotions` (kampanjer) samt `depositPrice` (pant).
- **NOT VERIFIED:** om priserna skiljer sig mellan butiker och mellan e-handel och fysisk butik.
  Ingen av klienterna hanterar butik (se nästa punkt).

### 4. Butiksval i Eskilstuna

- **OBSERVED** (jobbannonser och Jula-sida, inte källor från Willys): det finns minst butikerna
  "Willys Eskilstuna C" och "Willys Eskilstuna Smeden", och troligen en Willys vid Folkesta
  handelsplats.
- **OBSERVED:** willys-agent skickar `storeId=2110` (hårdkodat) bara till kategoriträdet.
  effati/willys-mcp har ingen butikshantering alls.
- **NOT VERIFIED:**
  - butiks-id för Eskilstuna-butikerna
  - hur butik väljs för sökning och varukorg (troligen genom sessionen eller kontot)
  - om valet påverkar sortiment och pris

  Butikssökaren på willys.se har inte använts. Det hade krävt automatiserade anrop mot samma
  interna API.
- Följd för spiken: `IProductCatalog` tar ingen butiksparameter. Uppdragets `--store`-flagga i
  CLI:t har strukits, eftersom det saknas en verifierad mekanism att koppla den till.

### 5. Varukorgshantering

- **OBSERVED (källkod):**
  - `GET /axfood/rest/cart` läser korgen
  - `POST /axfood/rest/cart/addProducts` lägger till eller sätter antal, med
    `{"products":[{"productCodePost","qty","pickUnit":"pieces",...}]}`
  - `qty: 0` tar bort en vara
  - `DELETE /axfood/rest/cart` tömmer korgen
  - ändrande anrop kräver `X-CSRF-TOKEN` från `/axfood/rest/csrf-token`
- **OBSERVED:** båda klienterna loggar in innan de rör korgen.
- **NOT VERIFIED:** om det finns en anonym korg (gästhandel finns enligt Willys hjälpsidor).
- **VERIFIED:** `robots.txt` har `Disallow: /varukorg` och `/kassa/`.
- Spikens `SimulatedCart` efterliknar operationerna (lägg till, sätt antal, `0` tar bort, töm)
  helt i minnet.

### 6. Autentisering

- **OBSERVED:** ErikHellman/willys-agent:
  - postar `j_username`/`j_password` till `POST /login`
  - båda fälten krypteras först med AES-128-CBC. Enligt källkoden är krypteringen en kopia av
    "module 89683" i Willys JS-bundle, alltså reverse engineering av Willys klientkod.
  - användarnamnet är **personnummer**
  - sessionen är cookie-baserad (`JSESSIONID`) med CSRF-token
- **OBSERVED:** effati/willys-mcp:
  - loggar in med en headless Chromium (go-rod), klickar "Acceptera" och "Logga in", fyller i
    personnummer eller Willys Plus-nummer och lösenord och kopierar sedan alla cookies
  - alla anrop skickar en **förfalskad webbläsar-User-Agent** (Chrome 120 på macOS) och
    `Sec-Fetch-*`-headrar
- **NOT VERIFIED:** om BankID eller någon form av delegerad åtkomst (OAuth eller liknande)
  finns. Inget tyder på det.
- Följd: för att agera åt en användare måste Hemordna antingen lagra personnummer och lösenord,
  eller hantera användarens sessionscookies. Det gäller alla kända vägar.

### 7. Användarvillkor

- **VERIFIED** ([evidence/villkor-2026-10-20.md](evidence/villkor-2026-10-20.md)): villkoren
  för Willys app förbjuder att man "dekonstruera[r], härled[er] koden, avkryptera[r]" och att
  man "kringgå[r] eventuella tekniska skyddsåtgärder". Licensen är personlig och
  icke-överlåtbar. Kunden ansvarar för att **ingen annan har tillgång till
  inloggningsuppgifterna**. Willys får stänga av konton som bryter mot villkoren.
- **VERIFIED:** `robots.txt` stänger sökning, varukorg, kassa och kontosidor för robotar och
  anger 10 s mellan anrop och tidsfönstret 04:00–08:45 UTC.
- **NOT VERIFIED:** e-handelsvillkoren. Sidan bygger upp innehållet med JavaScript och texten
  gick inte att läsa.
- Slutsats: inloggningen enligt willys-agent bygger på avkrypterad och kopierad klientkod. En
  tredje part (Hemordna) som håller användarens inloggningsuppgifter strider direkt mot kundens
  åtagande. Båda vägarna kolliderar med verifierad villkorstext, även utan e-handelsvillkoren.

### 8. Kommersiell användbarhet

- **OBSERVED:** inget officiellt eller publikt API och inget partnerprogram har hittats. API:t
  är internt och odokumenterat, och det finns inget avtal eller SLA.
- **INFERRED:** varje ändring i Willys webbklient (endpoints, kryptering, bot-skydd) kan bryta
  integrationen utan förvarning. Användarnas konton riskerar att stängas av.
- **INFERRED:** Hemordna skulle få persondataansvar för personnummer och lösenord, eller
  sessioner, hos en tredje part. Det är ett säkerhets- och persondatabeslut (CLAUDE.md §9), och
  inget i spiken motiverar det.

## Granskning av open source-projekten (read-only, ingen kod körd)

| | ErikHellman/willys-agent | effati/willys-mcp |
|---|---|---|
| Licens | MIT | MIT |
| Språk | TypeScript (Node 18+) | Go (go-rod) |
| Aktivitet | 11 commits, 10★ | 2 commits, 1★ |
| Sökning | `/search/clean` | samma parametrar (`q`, `page`, `size`) |
| Korg | `addProducts`, `qty:0`, `DELETE` | samma body, plus kassa och leveranstider |
| Inloggning | kopierad AES-kryptering från Willys JS-bundle, personnummer och lösenord i env | headless browser, cookies kopieras, personnummer och lösenord i env |
| User-Agent | runtimens standard | förfalskad Chrome-UA och `Sec-Fetch-*` |
| Rate limiting | ingen | ingen |
| Butik | `storeId=2110` hårdkodat (bara kategorier) | ingen |
| Villkor | nämns inte | nämns inte |
| Skörhet | hög: kopierad klientkod, hårdkodat id | hög: beror av DOM-text ("Logga in", "Acceptera") |

Även jimmystridh/willys-mcp har setts (MIT, Puppeteer-inloggning). Det har inte granskats på
djupet. Där står "Not affiliated with Willys or Axfood. Use responsibly."

## Matchning av testlistan (mot påhittad mock-katalog)

**Det här säger bara något om Hemordnas egen logik, inget om Willys.** Mock-katalogen
([../src/StoreSpike.Infrastructure/Fixtures/mock-catalog.json](../src/StoreSpike.Infrastructure/Fixtures/mock-catalog.json))
är uppfunnen. Dess sökning returnerar medvetet brus ("mjölkchoklad" för "mjölk",
"kaffefilter" för "kaffe") så att matchningen prövas.

| Rad | Tolkning | Matchning | Varukorg |
|---|---|---|---|
| nötfärs till två måltider | mängd saknas, anteckning "till två måltider" | välj: 12 % eller 5 % | ej tillagd |
| blandfärs | mängd saknas | Matchad | ej tillagd, mängd saknas |
| kycklingfilé | mängd saknas | Matchad (före "Panerad"; "-burgare" utesluten) | ej tillagd, mängd saknas |
| mjölk | mängd saknas | välj: lätt, mellan eller standard ("mjölkchoklad" utesluten) | ej tillagd |
| kaffe | mängd saknas | bekräfta: "Bryggkaffe" (lös träff; "kaffefilter" utesluten) | ej tillagd |
| makaroner | mängd saknas | Matchad (exakt slår "snabbmakaroner") | ej tillagd, mängd saknas |
| potatis | mängd saknas | välj: fast eller mjölig ("sötpotatis" bortprioriterad) | ej tillagd |
| ägg storlek L | storlek L, mängd saknas | Matchad: L, inte M (ekologiska L är slut) | ej tillagd, mängd saknas |

Simulerad varukorg: tom, summa 0,00 kr. Det är rätt resultat, eftersom ingen rad anger en mängd
och spiken inte hittar på några. Fyra av åtta rader kräver ändå ett aktivt val av användaren.

Kända begränsningar i matchningen:
- Reglerna för plural och sammansättningar är enkla. "L" kan förväxlas med liter i ett
  produktnamn.
- Verkliga Willys-namn har inte provats. Hur väl reglerna träffar på riktiga namn är därför
  NOT VERIFIED.

## Teknisk implementation

`StoreSpike.slnx` är en fristående lösning med .NET 10. Den ingår inte i `Hemordna.slnx`.

- **StoreSpike.Core**
  - `ShoppingListParser`: bara siffra plus enhet räknas som mängd.
  - `ProductMatcher`: huvudordet i en sammansättning står sist; en lös träff väljs aldrig
    automatiskt.
  - `SimulatedCart`: omvandlar aldrig mellan enheter.
  - `ShoppingListRun`
- **StoreSpike.Infrastructure:** `MockProductCatalog` och fixturen. Ingen klient för Willys,
  enligt beslutet ovan.
- **StoreSpike.Cli:** kör listan mot mock-katalogen och skriver rapporten.
- **StoreSpike.Tests:** xUnit 2.9.3, 45 tester:
  - mängdtolkning
  - matchning
  - varukorg
  - hela flödet med deterministisk rapport

Avvikelser från uppdraget:
- `WillysProductCatalog` har inte byggts.
- Parsningstestet mot ett sparat Willys-svar och stubbtestet av HTTP-klienten utgår, eftersom
  det inte finns något verkligt svar att testa mot.
- `--store` har strukits (se fråga 4).
