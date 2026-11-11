# STORE-SPIKE-01 – Beslut

Datum: 2026-10-20 · Underlag: [findings.md](findings.md)

## Beslut: **NO-GO**

Hemordnas inköpslista ska inte kopplas till Willys produktkatalog och digitala varukorg med
de vägar som finns i dag.

## Motivering

Kriteriet för NO-GO i uppdraget (§1) är uppfyllt på tre punkter, var och en tillräcklig för sig:

1. **Villkoren förbjuder automatiserad åtkomst.** Det är VERIFIED: `robots.txt` stänger
   `/sok` och `/varukorg` för robotar. Ingen annan väg till sökning eller varukorg är
   sanktionerad. Att använda de interna adresserna `/search/clean` och `/axfood/rest/cart` vore
   att kringgå avstängningen.
2. **Integrationen kräver reverse engineering eller kringgående av skydd.** Det är OBSERVED i
   källkod och VERIFIED mot villkoren. Inloggning kräver antingen Willys klientkryptering
   kopierad ur JS-bundlen, vilket strider mot förbudet att "härleda koden, avkryptera", eller
   en headless browser med förfalskad webbläsaridentitet.
3. **Hemordna skulle behöva hantera användarnas inloggningsuppgifter.** Personnummer och
   lösenord, eller sessionscookies, skulle ligga hos Hemordna. Det strider mot kundens åtagande
   att "ingen annan … har tillgång till dina inloggningsuppgifter". Det är också ett
   persondata- och säkerhetsansvar som inget i spiken motiverar (CLAUDE.md §9).

Kriterierna för GO och LIMITED GO kräver ett sanktionerat eller tillåtet gränssnitt. Något
sådant har inte hittats. HOLD passar inte: villkoren är inte oklara på de punkter som avgör
saken. Den enda villkorstext som inte kunnat läsas, e-handelsvillkoren, kan inte göra
punkterna ovan tillåtna.

## Vad spiken visar, och inte visar

- Tekniskt **finns** ett internt API som tredjepartsprojekt använder (OBSERVED). Det
  tekniska var aldrig det svåra. Det som saknas är lov och en säker auth-modell.
- Hemordnas egen logik fungerar mot en påhittad katalog: tolkning av listan, matchning och en
  simulerad korg som inte hittar på mängder. Det är verifierat med 45 tester, men det är inget
  bevis för att en verklig integration fungerar.
- Live-beteende, butiksval i Eskilstuna och hur stabila produktkoder och priser är har **inte**
  verifierats. Det kräver anrop som beslutet ovan säger att vi inte ska göra.

## Alternativ som prövats

| Alternativ | Bedömning |
|---|---|
| Interna API:t via open source-mönstren | **Nej.** Det är skälet till NO-GO. |
| Headless browser i användarens namn | **Nej.** Samma skäl, och dessutom förfalskad identitet. |
| Partneravtal eller officiellt API från Axfood | Den enda vägen till GO. Ingen sådan har hittats. Kräver en förfrågan till Axfood och ett eget beslut om Hemordna alls vill ha en butikskoppling. |
| Länk till Willys sökning i användarens egen webbläsare ("Sök på Willys") | Ingen integration och ingen automatisering. `robots.txt` gäller robotar, inte en användare som klickar. Kopplar inte listan till korgen. Länkformatet är NOT VERIFIED. |
| Ingen butikskoppling | Förenligt med nuvarande scope. |

## Konsekvens för scope (CLAUDE.md §12)

Spiken ger **inget skäl att öppna** scope-gränsen för shoppinglista med en butikskoppling som
argument. Om Hemordna ska ha en egen inköpslista är en separat produktfråga. Den ska avgöras på
eget behov (ett verkligt användningsfall först) och inte på en Willys-integration som inte är
tillåten.

## Vad som skulle ändra beslutet

- Axfood erbjuder ett dokumenterat partner-API eller ger skriftligt tillstånd, med delegerad
  inloggning (t.ex. OAuth) så att Hemordna aldrig ser användarens uppgifter. Då blir en ny
  spike aktuell, med live-verifiering av sökning, butiksval och varukorg.

## Rekommenderat nästa steg

Stäng spiken som NO-GO. Om en butikskoppling fortfarande är intressant tar Björn ett beslut om
att skicka en kort förfrågan till Axfood om partner-API eller tillstånd. Spiken skickar
ingenting själv. Ingen kod från spiken ska flyttas in i Hemordnas produktprojekt.
