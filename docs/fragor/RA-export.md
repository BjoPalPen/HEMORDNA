# RA-export

## RA-export · Kan en medlem lämna sitt hushåll själv och få ut sin egen data som JSON?

Fråga från Björn till Code, 2026-11-04. Ett förslag ur residualanalysen (stressorer s9, s14, s17, s25, s37, s43) ska kontrolleras mot koden innan det läggs in i en uppgift. Code ska inte bygga något och inte ändra någon uppgiftsfil. Svaret skrivs under "Svar" i den här filen.

### Förslaget

- Ny endpoint `GET /api/households/{id}/members/me/export` som ger en JSON-fil med medlemmens egna data: medlemsposten (visningsnamn, roll, tillgänglighet, lediga dagar, preferenser), egna påminnelser, uppgiftsförekomster som medlemmen slutfört eller är tilldelad, och de uppgiftsdefinitioner de hör till. Andra medlemmars påminnelser och personuppgifter ingår inte.
- Ny endpoint `POST /api/households/{id}/members/me/leave` som avaktiverar den egna medlemsposten med samma invariant som `DeactivateMember` (siste aktive managern får inte lämna) och kopplar bort kontot från hushållet, så att användaren kan skapa eller gå med i ett annat.
- Export och lämna är tillgängliga för alla aktiva medlemmar med konto, inte bara de som har `CanManageHousehold`.
- Det som uttryckligen inte görs: ingen import, ingen export av hela hushållet, ingen radering av konto, ingen ändring av regeln "en användare per hushåll".

### Kontrollera, och svara på varje punkt med fil och rad

1. **Vad som är "min data".**
   - Vilka entiteter har en koppling till `HouseholdMember` i domänen och i EF-konfigurationen? Lista dem med fil.
   - Vilka av dem är entydigt medlemmens egna, och vilka är delade med hushållet (till exempel `TaskDefinition`, `Area`)? Hur ska en förekomst som roterats mellan medlemmar räknas?
2. **Lämna.**
   - Kan `Household.DeactivateMember` återanvändas av medlemmen själv, eller förutsätter den och endpointgruppen `manage` att anroparen har `CanManageHousehold`?
   - Vad händer med `UserId` på en avaktiverad medlem i dag? Blockerar regeln "en användare per hushåll" (ARCHITECTURE.md §4) att användaren sedan skapar eller går med i ett nytt hushåll? Var sitter den kontrollen?
   - Vad händer med användarens `PushSubscription` och refresh-token vid lämnande?
3. **Behörighetsgrupperna.**
   - Hur är `manage` och `scoped` uppdelade i `HouseholdEndpoints.cs`? Passar `members/me/*` i `scoped`, eller behövs en tredje grupp?
4. **Formatet.**
   - Finns redan en JSON-serialisering i Api (System.Text.Json-inställningar, `DateOnly`/`TimeOnly`-hantering) som exporten bör följa? Kontrollera mot koden, inte ur minnet.
   - Hur stor kan en export bli för ett hushåll som använts ett år? Räcker ett vanligt svar, eller behövs strömning?
5. **Klienten.**
   - Var i Blazor-klienten hör knapparna hemma enligt DESIGN.md? Finns redan ett mönster för filnedladdning (`print.js` eller annat)?
6. **Integriteten.**
   - Kan en annan medlems titel, plats eller visningsnamn smita med i exporten via delade påminnelser (`ReminderVisibility.Household`) eller via `TaskAssignment`?
   - Bryter förslaget mot PRODUCT.md §8 (ingen skuldbeläggning) om exporten innehåller överhoppade förekomster?
7. **Bedömningen.**
   - Är förslaget värt att lägga i en uppgift, helt, delvis eller inte alls? Bör lämna och export vara två uppgifter?
   - Vilka prov skulle visa att det fungerar? Föreslå rader till provavsnittet. Minst: en export från medlem A får inte innehålla någon sträng ur medlem B:s privata påminnelser, och siste managern nekas att lämna med `DomainException`.

### Svar

(Code skriver här.)

**Väntar:** Björns beslut om omfång för "min data" och om lämna och export blir en eller två uppgifter.
