# RA-push

## RA-push · Kan push få en avstängningsflagga och en text som tål att hamna hos fel person?

Fråga från Björn till Code, 2026-11-04. Ett förslag ur residualanalysen (stressorer s4, s20, s47, s56) ska kontrolleras mot koden innan det läggs in i en uppgift. Code ska inte bygga något och inte ändra någon uppgiftsfil. Svaret skrivs under "Svar" i den här filen.

### Förslaget

- En konfigurationsflagga `Push:Enabled` (standard `true`) som `ReminderPushBackgroundService` läser vid varje varv. Är den `false` skickas inget, men `SentReminderNotification` skrivs inte heller, så att inget går förlorat när flaggan slås på igen. Flaggan ska gå att ändra utan omstart om det är billigt, annars med omstart.
- Platsen tas bort ur pushtexten. Titeln behålls, enligt PRODUCT.md §11. Avgångstiden behålls.
- Det som uttryckligen inte görs: ingen ny kanal (e-post eller in-app-lista), ingen generell feature-flag-infrastruktur, ingen ändring av VAPID-hanteringen.

### Kontrollera, och svara på varje punkt med fil och rad

1. **Var texten byggs.**
   - Visa `SendDueReminderNotifications.BuildText` och vilka fält ur `DueReminderNotification` som hamnar i texten i dag. Ingår `Location`?
   - Finns ett prov i `Hemordna.Application.Tests` som låser texten, så att ändringen syns?
2. **Flaggan.**
   - Hur läses konfiguration i `ReminderPushBackgroundService` i dag – `IOptions`, `IOptionsMonitor` eller direkt? Vilket av dem ger ändring utan omstart i den nuvarande Docker-uppsättningen med miljövariabler? Kontrollera mot källan, inte ur minnet.
   - Var i varvet ska flaggan läsas så att varken `SentReminderNotification` skrivs eller prenumerationer rensas när push är avstängd?
3. **Vad som händer med köade notiser.**
   - Om push varit av i tre timmar och slås på: skickas då alla missade "dags att gå", eller filtrerar `ReminderNotificationSelector` bort passerade tider? Vilket beteende är önskvärt enligt PRODUCT.md §11 ("en passerad tid blir tyst")?
4. **Felriktade notiser.**
   - Hur kopplas en `PushSubscription` till en medlem, och var kontrolleras att prenumerationen hör till rätt `MemberId` innan utskick? Finns någon väg där en prenumeration kan överleva ett byte av konto på samma enhet?
5. **Klienten.**
   - Visar klienten något när push är avstängt på servern, eller ser det ut som om notiser fungerar? Var sitter `isSupported()` i `push.js` och vad gör inställningssidan av svaret?
6. **Integriteten.**
   - Bryter borttagningen av platsen mot något i PRODUCT.md §11 eller DESIGN.md? Hamnar titel eller plats i någon logg vid utskick eller fel?
7. **Bedömningen.**
   - Är förslaget värt att lägga i en uppgift, helt, delvis eller inte alls?
   - Vilka prov skulle visa att det fungerar? Föreslå rader till provavsnittet. Minst: med `Push:Enabled=false` görs inget anrop mot `IPushSender` och ingen rad skrivs i `SentReminderNotification`; texten för en påminnelse med plats innehåller inte platsen.

### Svar

(Code skriver här.)

**Väntar:** Björns beslut om flaggan ska kunna ändras utan omstart, och om platsen ska bort ur texten.
