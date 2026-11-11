# STORE-SPIKE-01 – Willys-integration för Hemordna Inköp

Agentuppdrag för en tidsboxad teknisk spike. Uppdraget gäller en utvärdering, inte en
produktfunktion. Ingen kod från spiken får flyttas in i Hemordnas produktprojekt utan ett
separat beslut.

| | |
|---|---|
| Branch | `spike/store-spike-01-willys` (från `main`) |
| Katalog | `spikes/STORE-SPIKE-01-Willys/` |
| Beställare | Björn |
| Status | Specifikation – implementation väntar på uttryckligt godkännande |

---

## 1. Syfte och beslutsfråga

> Kan Hemordnas inköpslista kopplas till Willys produktkatalog och digitala varukorg på ett
> sätt som är tekniskt möjligt, tillåtet enligt Willys villkor och lämpligt för Hemordna?

En fungerande mock räcker inte som bevis. Mocken är grunden för logiken, men varje påstående
om Willys måste bygga på observerade svar, dokumenterade villkor eller granskad källkod, och
märkas efter hur säkert det är (se §8).

Bakgrund: shoppinglista står i dag som out of scope i `CLAUDE.md` §12 och `docs/PRODUCT.md`.
Spiken ändrar inte det. `decision.md` ska däremot ta ställning till om underlaget motiverar att
gränsen omprövas.

### Slutbeslut

`docs/decision.md` ska sluta i exakt ett av följande beslut:

| Beslut | Kriterier |
|---|---|
| **GO** | Det finns ett sanktionerat gränssnitt (officiellt API, partneravtal eller uttryckligt tillstånd) för både sökning och varukorg. Auth går att lösa utan att Hemordna lagrar användarens Willys-lösenord eller personnummer, t.ex. med OAuth eller delegerad åtkomst. |
| **LIMITED GO** | En begränsad integration är tillåten och robust, t.ex. produktsökning och prisvisning, eller deeplink eller export av listan som användaren själv tar med till Willys. Varukorgen hanteras manuellt av användaren. |
| **HOLD** | Det tekniska fungerar men villkoren är oklara, eller ett svar från Axfood behövs. Ett konkret nästa steg ska anges, t.ex. en förfrågan till Axfood. |
| **NO-GO** | Villkoren förbjuder automatiserad åtkomst, eller integrationen kräver reverse engineering, kringgående av skydd eller att Hemordna hanterar användarnas inloggningsuppgifter. |

Beslutet motiveras punkt för punkt mot frågorna i §2.

---

## 2. Frågor att undersöka

Varje fråga besvaras i `docs/findings.md` med en faktaetikett och en hänvisning till bevis.

1. **Produktsökning:** går det att söka på fritext, t.ex. "nötfärs"? Vilket gränssnitt används,
   hur ser svaret ut, och finns paginering och sortering?
2. **Produktidentifiering:** vilken stabil nyckel har en produkt? Är det Willys `code` (t.ex.
   `101175556_ST`) eller EAN/GTIN? Är nyckeln stabil över tid och mellan butiker?
3. **Priser:** vilka priser visas, t.ex. styckpris, jämförpris (kr/kg, kr/l) och
   kampanjpris? Skiljer de sig mellan butiker och mellan e-handel och fysisk butik?
4. **Butiksval i Eskilstuna:** vilka Willys-butiker finns i Eskilstuna och vilka id har de?
   Hur väljs butik i sök- eller korgflödet? Påverkar butiksvalet sortiment och pris?
5. **Varukorgshantering:** hur läggs varor till, hur ändras antal och hur tas de bort?
   Krävs inloggning? Finns en anonym korg?
6. **Autentisering:** vilken inloggningsmodell används (lösenord, BankID, session, CSRF)? Vad
   skulle Hemordna behöva lagra för att agera för en användare? Finns delegerad åtkomst?
7. **Användarvillkor:** vad säger Willys och Axfoods användarvillkor och köpvillkor om
   automatiserad åtkomst, scraping och tredjepartsappar? Vad säger `robots.txt`?
8. **Kommersiell användbarhet:** kan Hemordna som produkt bygga på integrationen? Det gäller
   stabilitet, avtal, SLA, ansvar vid fel och vilken support användaren kan räkna med.

---

## 3. Kunskapsläge vid uppdragets start

Research gjordes under planeringen och ska verifieras och fördjupas i spiken.

- **OBSERVED:** inget officiellt, publikt API från Willys eller Axfood har hittats.
- **OBSERVED:** [ErikHellman/willys-agent](https://github.com/ErikHellman/willys-agent)
  (MIT, TypeScript) anropar ett internt API på `https://www.willys.se`:
  - `GET /search/clean?q=&size=&page=` – sökning
  - `GET /leftMenu/categorytree?storeId=2110&deviceType=OTHER` – kategorier
  - `GET /c/{categoryPath}` – kategoribläddring
  - `GET /axfood/rest/csrf-token`, `GET /api/config` – session och CSRF
  - `POST /login` med `j_username`/`j_password`, AES-128-CBC-krypterade på klientsidan.
    Krypteringen är enligt källkoden en kopia av "module 89683" i Willys JS-bundle, alltså
    reverse engineering.
  - `GET /axfood/rest/cart`, `POST /axfood/rest/cart/addProducts`, `DELETE /axfood/rest/cart`
    – varukorg. `addProducts` tar `productCodePost`, `qty` och `pickUnit`, och `qty: 0` tar
    bort en vara.
- **OBSERVED:** [effati/willys-mcp](https://github.com/effati/willys-mcp) (MIT) loggar in med
  en headless browser, accepterar cookies och plockar ut sessionscookies. Det kräver
  personnummer eller Willys Plus-nummer samt lösenord.
- **OBSERVED:** `https://www.willys.se/robots.txt`:
  - `Disallow: /kassa/`, `/varukorg`, `/mitt-konto/`, `/mina-kop/`, `/mina-listor/`,
    `/delad-lista/`, `/minavanligastevaror`, `/o/` och `/sok`
  - `Crawl-delay: 10`
  - `Visit-time: 0400-0845` (UTC)
  - `/search/clean` och `/axfood/rest/*` nämns inte uttryckligen.
- **NOT VERIFIED:**
  - Willys användarvillkor. Sidan är en SPA och villkorstexten är inte lokaliserad.
  - Hur butik väljs. Endast `storeId` i kategoriträdet har setts.
  - Om något av ovanstående fortfarande fungerar.

---

## 4. Teknik

Fristående .NET 10-lösning `spikes/STORE-SPIKE-01-Willys/StoreSpike.slnx`. Den läggs **inte**
in i `Hemordna.slnx` och refererar inga Hemordna-projekt.

```text
spikes/STORE-SPIKE-01-Willys/
  StoreSpike.slnx
  src/
    StoreSpike.Core/            inköpslista, mängdtolkning, matchning, simulerad varukorg
    StoreSpike.Infrastructure/  MockProductCatalog + WillysProductCatalog (endast läsning)
    StoreSpike.Cli/             kör listan mot vald katalog, skriver rapport
  tests/
    StoreSpike.Tests/           xUnit
  docs/
    findings.md
    decision.md
    evidence/                   sparade råsvar och villkorsutdrag
```

### Ansvar per projekt

- **Core:** inga externa paket och inga HTTP-beroenden.
  - `ShoppingListItem`: originaltext, tolkad vara, mängd och enhet. Mängd och enhet är
    nullable.
  - `QuantityParser`: tolkar explicita mängder som "2 st", "500 g" och "1,5 l". Saknas mängd
    blir den `null`.
  - `StoreProduct`: `Code`, namn, varumärke, förpackningsstorlek, pris, jämförpris och
    jämförenhet, plus EAN om den finns.
  - `IProductCatalog`: sökning per fritext och butik. Det är den verkliga gränsen mellan mock
    och Willys och den enda abstraktionen som motiveras.
  - `ProductMatcher`: returnerar `Matched`, `NeedsUserChoice` (tvetydigt, med kandidater) eller
    `NotFound`.
  - `SimulatedCart`: lägg till, ändra antal, ta bort, töm och summera. Rader utan bekräftad
    mängd avvisas.
- **Infrastructure:**
  - `MockProductCatalog` läser fixturer. Gärna en fixtur med verklig svarsform när ett sådant
    svar finns i `docs/evidence/`.
  - `WillysProductCatalog` använder `HttpClient` och `System.Text.Json` mot sökgränssnittet.
    Den får **bara** göra oautentiserade GET-anrop. Varukorgsanrop implementeras inte.
- **Cli:**
  - `--catalog mock|willys` och `--store <id>`.
  - Skriver en matchningsrapport per rad (status, vald produkt, kandidater, mängdstatus) och en
    simulerad korg.
  - Live-läget kräver en explicit flagga och följer begränsningarna i §6.
- **Tests:** xUnit med samma paket och versioner som
  `tests/Hemordna.Application.Tests/Hemordna.Application.Tests.csproj` (xunit 2.9.3,
  xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4).

### Konventioner

- Följ Hemordnas CLAUDE.md: nullable på, ImplicitUsings, `async`/`await` och
  `CancellationToken` genom I/O, ingen `DateTime.Now` i logiken.
- Inga spekulativa abstraktioner och inget interface per klass.
- Kod och identifierare skrivs på engelska, dokumentationen på svenska.
- Inga nya NuGet-paket utöver testpaketen ovan utan motivering i `findings.md`.

### OSS-granskning

Granska ErikHellman/willys-agent och effati/willys-mcp **read-only**. Klona eller kör inte
deras kod mot Willys. Dokumentera:

- licens
- senaste aktivitet
- endpoints
- auth-flöde
- felhantering
- vad som tyder på skörhet (hårdkodade id, kopierad klientkod, headless-inloggning)
- om projekten tar ställning till villkoren

---

## 5. Testdata

Svensk inköpslista som ska köras genom hela flödet:

```text
nötfärs till två måltider
blandfärs
kycklingfilé
mjölk
kaffe
makaroner
potatis
ägg storlek L
```

**Mängder som saknas får inte hittas på.**

- Ingen rad i listan har en explicit mängd. Därför får alla `Quantity = null` och status
  `QuantityMissing`.
- Korgen får inte lägga till en vara utan bekräftad mängd. Rapporten visar vad användaren
  behöver ange.
- "till två måltider" är ett användningssyfte, inte en vikt. Det sparas som anteckning och
  omvandlas aldrig till t.ex. 1 kg eller 2 förpackningar.
- "storlek L" är ett produktattribut som ska påverka matchningen (L, inte M eller S). Det är
  ingen mängd.
- Förpackningsstorlek från katalogen, t.ex. "1,5 l" eller "500 g", beskriver produkten och får
  inte tolkas som användarens önskade mängd.

---

## 6. Säkerhetsregler

Reglerna är absoluta och gäller hela spiken. Att avvika från dem kräver ett **separat,
uttryckligt godkännande** från Björn för just den åtgärden.

- Inga riktiga kontouppgifter: inget personnummer, inget lösenord, inget Willys Plus-nummer,
  varken i kod, config, env-filer, loggar eller dokumentation.
- Inga köp, inga beställningar, ingen betalning och inget val av leveranstid.
- Inga ändringar i verkliga varukorgar.
- Inget kringgående av åtkomstskydd:
  - ingen headless-inloggning
  - ingen återskapad klientkryptering
  - ingen förfalskad eller webbläsarimiterande User-Agent
  - ingen IP-rotation
  - inget kringgående av rate limits, captcha eller bot-skydd
- **Live-läsning** sker bara i den här ordningen:
  1. Villkoren läses och dokumenteras i `docs/evidence/`. Förbjuder de automatiserad
     åtkomst görs inga live-anrop. Det är i sig ett resultat.
  2. Björn godkänner live-körningen.
  3. Körningen följer dessa gränser:
     - högst cirka 20 oautentiserade GET per körning
     - minst 10 s mellan anropen, enligt `Crawl-delay`
     - helst inom `Visit-time`
     - ärlig User-Agent som identifierar spiken
  4. Vid 403, 429, captcha eller annat tecken på blockering avbryts körningen direkt och
     händelsen dokumenteras.
- Råsvar som sparas i `docs/evidence/` får inte innehålla cookies, sessions-id eller
  personuppgifter.

---

## 7. Föreslagna tester

**Mängdtolkning (`QuantityParser`)**

- "2 st", "500 g", "1,5 l" och "1.5 l" tolkas till rätt mängd och enhet.
- "nötfärs till två måltider" ger `Quantity = null` och `QuantityMissing`, med "till två
  måltider" kvar som anteckning.
- Ingen rad i testlistan får ett påhittat antal (gäller alla åtta rader).
- "ägg storlek L" ger ingen mängd. "L" blir ett attribut och inte liter.

**Matchning (`ProductMatcher`)**

- Normalisering: versaler/gemener, å/ä/ö och enkel plural ("ägg").
- "ägg storlek L" matchar en L-förpackning och inte M.
- "kycklingfilé" matchar inte "kycklingfiléburgare" eller "kycklingfilé panerad" som förstaval.
- Flera rimliga träffar, t.ex. "mjölk" (standard, mellan, lätt), ger `NeedsUserChoice` med
  kandidaterna.
- Ingen träff ger `NotFound` och aldrig en slumpad produkt.
- Ordningen är deterministisk vid lika poäng.

**Simulerad varukorg (`SimulatedCart`)**

- Det går att lägga till en vara, ändra antal, ta bort den och tömma korgen.
- Två tillägg av samma `Code` summerar antalet.
- Antal 0 tar bort raden, och negativt antal avvisas.
- Totalsumman räknas rätt för styckvaror och viktvaror.
- En rad med `QuantityMissing` avvisas.

**Infrastructure**

- `WillysProductCatalog` parsar ett **sparat verkligt** svar från `docs/evidence/`, aldrig ett
  påhittat JSON som utger sig för att vara Willys. Saknade eller nya fält hanteras utan krasch.
  Om inget live-svar får hämtas utgår testet, och det dokumenteras.
- `WillysProductCatalog` testas med en `HttpMessageHandler`-stub. Testet ska visa att endast
  GET skickas, att User-Agent sätts och att 403/429 ger ett tydligt fel.

**Cli**

- Hela flödet körs mot `MockProductCatalog` med testlistan och ska ge en deterministisk rapport.

---

## 8. Faktaetiketter

Använd samma etiketter som Hemordna (CLAUDE.md §6), med ett tillägg för spiken:

| Etikett | Betydelse |
|---|---|
| `VERIFIED` | Har körts eller lästs i spiken, med bevis i `docs/evidence/` |
| `OBSERVED` | Har setts i källkod, dokumentation eller `robots.txt`, men inte provats |
| `ASSUMED` | Står bara i tredjepartskod eller härleds, och är ej bekräftat mot Willys |
| `NOT VERIFIED` | Har inte kunnat undersökas, med orsak |

Mocken bevisar bara att Hemordnas egen logik fungerar. Den får aldrig användas som belägg för
något om Willys.

---

## 9. Leverabler

1. Teknisk implementation enligt §4.
2. Automatiska tester enligt §7. `dotnet build` och `dotnet test` mot `StoreSpike.slnx`
   ska ha körts, och utfallet redovisas exakt.
3. `docs/findings.md`: svar på varje fråga i §2 med faktaetikett och bevis, OSS-granskningen,
   matchningsresultat för testlistan (mot mock och, om det godkänts, mot live-sökning) samt
   villkorsanalysen.
4. `docs/decision.md`: slutbeslut (GO, LIMITED GO, HOLD eller NO-GO) enligt §1. Ska innehålla
   motivering, alternativ (deeplink, export av listan, förfrågan om partnerskap hos Axfood,
   ingen integration), konsekvens för scope-gränsen i CLAUDE.md §12 och ett konkret nästa steg.
5. `docs/evidence/`: villkorsutdrag och sanerade råsvar.

---

## 10. Avgränsningar

- Ändra inte befintliga Hemordna-projekt i `src/` och `tests/`.
- Ändra inte databasmigreringar, databasen, `Hemordna.slnx` eller CI.
- Allt arbete sker under `spikes/STORE-SPIKE-01-Willys/`.
- Ingen push och ingen PR utan Björns OK. Commits ska vara små, i conventional commit-form,
  t.ex. `docs(spike): add STORE-SPIKE-01 agent assignment`.
- Verifiering: `dotnet build Hemordna.slnx` före och efter för att visa att huvudlösningen är
  orörd. `git status` ska bara visa ändringar under `spikes/`.

---

## 11. Arbetsordning och checkpoints

1. Läs CLAUDE.md, `docs/PRODUCT.md` och `docs/ARCHITECTURE.md` (de delar som rör "Handla
   mat"), och detta uppdrag.
2. Läs villkoren och dokumentera dem i `docs/evidence/`.
3. Granska OSS-projekten.
4. Bygg Core och Tests test-först, sedan Infrastructure (mock) och Cli.
5. **Checkpoint A:** stoppa och be om godkännande för live-sökning enligt §6. Det gäller även
   uppslag av butiker i Eskilstuna.
6. Kör live-sökningen om den godkänts och spara sanerade råsvar som fixturer.
7. **Checkpoint B:** autentiserad varukorg rekommenderas **inte**. Lämpligheten avgörs av
   auth-modellen, och den kan analyseras utan inloggning. Ska steget ändå göras krävs ett
   separat godkännande, och då loggar Björn in själv på sitt eget konto. Agenten hanterar
   aldrig inloggningsuppgifter.
8. Skriv `findings.md` och `decision.md` och rapportera enligt CLAUDE.md §15.
