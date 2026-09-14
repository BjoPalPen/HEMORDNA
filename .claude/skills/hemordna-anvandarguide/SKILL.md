---
name: hemordna-anvandarguide
description: Underhåller Hemordnas HTML-användarguider under wwwroot/hjalp - hubben, familjeguiden, ansvarigguiden, den delade mallen och de mobila skärmbilderna. Används när en guide ska skrivas om, ett kapitel läggas till, eller skärmbilderna tas om efter en UI-ändring. Triggers - "användarguide", "guide", "hjälp", "hjalp", "skärmbilder till guiden", "uppdatera guiden", "user guide", "help page", "screenshots".
disable-model-invocation: true
---

# Hemordna – Användarguider

Två guider under `src/Hemordna.Client/wwwroot/hjalp/`, publikt nåbara på
`app.hemordna.se/hjalp/` och länkade från Hushållsöversikten.

Kör den här **bara när det uttryckligen begärts.** Att ta om 22 skärmbilder och skriva om
guider är inget som ska hända som sidoeffekt av något annat.

Beslut och bakgrund: `docs/ARCHITECTURE.md` "Beslut: Användarguider under /hjalp",
form och ton: `docs/DESIGN.md` §11.

---

## Filkarta

```
src/Hemordna.Client/wwwroot/hjalp/
  index.html                 hubb med rollkort (handredigerad)
  assets/guide.css           delad stil - RÖR EJ utan skäl
  assets/guide.js            delad mall som bygger layout, TOC och kapitel
  sv/familjen.html           guide för alla i familjen
  sv/hushallsansvarig.html   guide för den som har krysset
  img/mobil_<namn>.png       skärmbilder, genererade

tests/Hemordna.E2E.Tests/
  GuideSkarmbilderTests.cs   tar skärmbilderna
  GuideRenderTests.cs        vaktar att guiderna faktiskt renderar
```

## Rollindelningen är inte påhittad

`sv/familjen.html` är för alla. `sv/hushallsansvarig.html` är för den som har
`CanManageHousehold`. Det är **samma gräns som koden redan drar** - se
"Beslut: Vem får ändra vad". Flyttas den gränsen i koden ska guiderna flytta med.

Familjeguiden har därför ett eget kapitel som förklarar *varför* vissa knappar inte syns.
Den låtsas aldrig att de inte finns, och skuldbelägger aldrig den som saknar dem
(`PRODUCT.md` §8).

---

## Skriva eller ändra ett kapitel

Varje guidesida är ett tunt skal som definierar `window.GUIDE` och laddar mallen sist. Sidan
bär bara innehåll - aldrig sin egen layout.

```js
window.GUIDE = {
    title: "För alla i familjen",
    subtitle: "Användarguide",
    intro: "En mening om vem guiden är för.",
    fakta: ["6 kapitel", "Tagna på 390 × 844"],
    sections: [
        { id: "idag", t: "Din dag", html: "<p>…</p>" }
    ],
    footer: "<p>…</p>"
};
```

Kapitlen numreras automatiskt ("Steg 1", "Steg 2"), och innehållsförteckningen byggs ur
`sections`. Lägg aldrig till egen numrering i `t`.

Tillgängliga klasser: `.gor` (numrerade steg), `.shots` + `.telefon` (skärmbilder),
`.notis` / `.notis.tips` / `.notis.obs`, `.ui` (en knapp som den heter i appen), `.ja` / `.nej`
i tabeller. Tabeller lindas automatiskt av mallen - skriv bara `<table>`.

### Regler som måste hålla

- **Form följer Hemordna, inte BowlingPlatform.** Strukturen är lånad; färg och typografi
  kommer ur `DESIGN.md` §2/§4.
- **Ingen extern font-CDN.** `guide.css` `@font-face`-ar appens egna filer i `../../fonts/`.
  Detta är ett skrivet beslut i `DESIGN.md` §4 och gäller guiderna lika mycket som appen.
- **En bild i taget på mobil.** Två i bredd krymper en 390px-skärmbild till ~170px och texten
  i bilden blir oläslig - vilket gör en mobilguide meningslös.
- **Skriv vad appen gör, aldrig vad användaren borde ha gjort** (`PRODUCT.md` §8).

---

## Ta om skärmbilderna

`GuideSkarmbilderTests` fångar 22 vyer på 390 × 844. Den följer samma opt-in som
`SkarmbilderTests`: skriver till `HEMORDNA_SCREENSHOT_DIR` om den är satt, annars en
temp-katalog - så en vanlig testkörning aldrig skriver om filer i källträdet.

```bash
HEMORDNA_SCREENSHOT_DIR=src/Hemordna.Client/wwwroot/hjalp/img \
  dotnet test tests/Hemordna.E2E.Tests --filter FullyQualifiedName~GuideSkarmbilderTests
```

Kräver att Docker Desktop kör och att containern `hemordna-db` är healthy.

**Filnamnen är guidernas kontrakt.** `sv/*.html` refererar dem vid namn - byter du namn i
testet måste du byta i guiden också. `GuideRenderTests` fångar en bruten sökväg.

---

## Verifiering innan du är klar

```bash
dotnet build Hemordna.slnx                                   # 0 warnings
dotnet test tests/Hemordna.E2E.Tests --filter FullyQualifiedName~GuideRenderTests
dotnet test tests/Hemordna.E2E.Tests --filter FullyQualifiedName~HushallTests
```

`GuideRenderTests` kontrollerar att mallen kört (kapitel finns och matchar TOC), att varje
skärmbild resolverar, och att ingen sida rullar i sidled på 390px. `HushallTests` täcker
länken in från appen.

> **Räkna aldrig trasiga bilder med `naturalWidth === 0`.** Varje skärmbild är
> `loading="lazy"`, så allt under vikningen är legitimt oladdat när sidan just öppnats - den
> kontrollen rapporterar falska fel. Hämta varje `src` och läs svarskoden i stället.

---

## Fällan som kostade en trasig produktion

Guiderna är **statiska filer, inte Blazor-routes.** Två saker måste gälla, och ingen av dem
syns i en vanlig testkörning:

1. **Länken in behöver `target="_blank"`.** Utan det fångar Blazor-routern klicket och letar
   efter en route som inte finns.

2. **Service workern får inte kapa navigeringen.** `service-worker.published.js` svarar med
   appskalet på varje `event.request.mode === 'navigate'`. Utan undantaget för `/hjalp/` får
   användaren "Sidan finns inte" medan guiden ligger oöppnad på servern.

Punkt 2 nådde produktion en gång. Den gick inte att se, av tre skäl samtidigt:

| Kontroll | Varför den var grön ändå |
|---|---|
| E2E-sviten | Dev-värden kör den tomma `service-worker.js`. Den publicerade finns bara i en publicerad build. |
| `curl` mot produktion | Går förbi service workern helt - filen fanns och svarade 200. |
| Visuell granskning | Guiden renderade perfekt när man öppnade den direkt. |

**Slutsats: en grön svit bevisar inte att PWA:n fungerar.** Efter en ändring som rör
`service-worker.published.js`, `wwwroot/hjalp/` eller länken in - öppna guiden i den
installerade appen på en telefon, eller kör en webbläsare mot den driftsatta sajten som
registrerar service workern. Inget annat täcker det.

---

## Fällan nummer två: grid och chips

`.gor li` (ett "så här gör du"-steg) får **inte** vara `display: grid` eller `flex`.

Ett steg innehåller `<span class="ui">`-chips mitt i meningen. I grid blir varje barnelement
ett eget grid-item - chipet hamnade i gutterkolumnen, klämt till 26px, medan resten av
meningen låg i nästa spår. På telefon blev det text ovanpå text. Siffran läggs därför ut
absolut och steget är vanligt textflöde.

Detta nådde också produktion, och också med grön svit. Lärdomarna:

- **En helsidesbild bevisar ingenting.** Guidens egen helsida är ~9900px hög; nedskalad till
  granskningsbar storlek går texten inte att läsa, och felet syntes inte. Skärmklipp **ett
  element i taget** (`locator.ScreenshotAsync`) när layout ska granskas.
- **Sidans bredd fångar det inte.** `document.body.scrollWidth` var oförändrad - chipets box
  var smal, det var bläcket inuti som spillde.
- **Mät det som är fel, inte något närliggande.** Två första försök (`scrollWidth >
  clientWidth`, och överlapp mellan elementens boxar) passerade med den trasiga CSS:en kvar.
  Kör alltid testet mot den trasiga versionen innan du litar på det.

`GuideRenderTests.No_step_overflows_its_own_row` vaktar detta nu, vid två textstorlekar.

---

## Inför CI

Det som går att köra i CI redan idag:

```bash
dotnet build Hemordna.slnx
dotnet test tests/Hemordna.E2E.Tests --filter FullyQualifiedName~GuideRenderTests
```

Det som **inte** täcks och bör byggas när CI sätts upp: ett steg som publicerar klienten
(`dotnet publish`), serverar publiceringsutdata statiskt och kör en webbläsare mot den, så
den riktiga `service-worker.published.js` faktiskt registreras. Det är det enda som fångar
regressioner i PWA-beteendet - offline-start, cachning och navigeringar utanför SPA:n.

Skärmbildspasset hör **inte** hemma i CI: det skriver filer i källträdet och ska köras
medvetet av en människa när gränssnittet ändrats.
