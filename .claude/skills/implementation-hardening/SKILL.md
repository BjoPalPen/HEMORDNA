---
name: implementation-hardening
description: Obligatoriskt arbetsläge vid all implementation i Hemordna. Skyddar mot hallucinationer, påhittade API-signaturer, ogrundade antaganden och falsk verifiering av build, tester, migrationer eller API-anrop. Används innan kod skrivs och innan status rapporteras. Triggers - all kodändring, "verifiera", "fungerar det", "är det klart", "verify", "does it work", "is it done", "run the tests".
---

# Implementation Hardening – Hemordna

Obligatorisk vid all implementation. Syftet är att det som rapporteras är sant.

## 1. Repository-grounding

Innan design eller kod, verifiera faktiskt tillstånd:

```bash
git branch --show-current && git status --short
cat Hemordna.slnx
find src tests -name '*.csproj' -exec cat {} +
```

Läs den kod du tänker ändra eller anropa. Anta aldrig att en klass, metod, property,
namespace, projektreferens eller testfil finns – öppna den.

## 2. Versions- och paketverifiering

Använd ingen feature utan rimlig säkerhet om att projektets faktiska versioner stöder den.

```bash
dotnet --version              # SDK
dotnet list <proj> package    # faktiska paketversioner
```

- Hitta inte på metodsignaturer, överlagringar eller paketnamn. Är du osäker: läs koden,
  låt kompilatorn avgöra, eller slå upp det – gissa inte.
- Lägg inte till ett NuGet-paket vars namn och version inte kunnat bekräftas.

## 3. Faktaetiketter

Använd dem i rapportering när det finns någon tvekan:

| Etikett | Betydelse |
|---|---|
| `OBSERVED` | finns faktiskt i repo eller i ett verktygsresultat |
| `INFERRED` | rimlig slutsats, inte explicit verifierad |
| `PROPOSED` | föreslagen förändring, ännu inte implementerad |
| `VERIFIED` | faktiskt körd eller kontrollerad, med observerat resultat |
| `NOT VERIFIED` | kunde inte verifieras |

## 4. Falsk verifiering är förbjuden

Säg **aldrig** att build passerar, tester passerar, migration fungerar, databasen
uppdateras, API:t svarar eller SignalR-synk fungerar utan att kommandot faktiskt körts och
utdata observerats. Citera kommandot och resultatet.

Verifieringskedja när miljön tillåter:

```bash
dotnet restore
dotnet build Hemordna.slnx
dotnet test Hemordna.slnx
```

- **Migration:** skapa den, *läs den genererade filen*, kontrollera att inget `DropColumn`
  eller `DropTable` smugit sig in, applicera i dev.
- **API:** starta processen och anropa endpointen. Ett kompilerande endpoint är inte ett
  verifierat endpoint.
- **SignalR/realtid:** verifieras med två faktiska anslutningar, annars `NOT VERIFIED`.

Går något inte att köra i miljön: rapportera det som `NOT VERIFIED` med skäl. Det är ett
fullt acceptabelt resultat. Att påstå PASS är det inte.

## 5. Antaganden

- Litet och reversibelt: välj konservativt, dokumentera antagandet i svaret.
- Rör det security, tenant isolation, auth, betalningar, persondata, irreversibel
  datamodell eller större backward compatibility: improvisera inte. Dokumentera
  blockeringen eller föreslå en säker väg framåt.

## 6. Diff review innan rapport

```bash
git status --short && git diff --stat && git diff
```

Leta efter: filer du inte tänkte röra, borttagen kod, ändrade csproj/paket,
scope creep, kvarglömd debugkod, secrets.

## 7. Stop conditions

Stoppa och rapportera: obrutbar build, overifierbar paketversion, migration som riskerar
dataförlust, secrets i repot, saknat auth-/securitybeslut, oklara externa ändringar, eller
en instruktion som motsäger repositoryts faktiska design på ett kritiskt sätt.

Stoppa inte för små reversibla beslut.
