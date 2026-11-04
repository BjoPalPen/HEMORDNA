# RA-backup

## RA-backup · Kan backuperna få en kopia utanför servern och ett larm, utan att privata nyckeln lämnar Björns maskin?

Fråga från Björn till Code, 2026-11-04. Ett förslag ur residualanalysen (stressorer s1, s5, s26, s48) ska kontrolleras mot koden innan det läggs in i en uppgift. Code ska inte bygga något och inte ändra någon uppgiftsfil. Svaret skrivs under "Svar" i den här filen.

### Förslaget

- `scripts/db-backup/backup-db.sh` får ett sista steg som kopierar den färdiga `.sql.gz.gpg`-filen till en plats utanför servern (objektlagring hos en annan leverantör än driftsleverantören, eller en annan maskin), med samma retention på 30 dagar där.
- Skriptet skriver ett "senast lyckad backup"-märke (tidsstämpel) som en extern kontroll kan läsa. Larm skickas om märket är äldre än 36 timmar. Larmvägen ska inte gå via appens egen e-post eller push, eftersom de kan vara nere av samma orsak.
- En kvartalsvis rutin för teståterställning dokumenteras i `scripts/db-backup/README.md`: återställ senaste dumpen till en tom databas i en container, räkna rader i `Households`, `HouseholdMembers`, `TaskDefinitions`, `TaskOccurrences`, jämför mot produktion.
- Det som uttryckligen inte görs: ingen ändring av krypteringen, ingen privat nyckel på servern, ingen ändring i `Hemordna.Api`.

### Kontrollera, och svara på varje punkt med fil och rad

1. **Kedjan i skriptet.**
   - Var i `backup-db.sh` är det säkert att lägga kopieringssteget så att en misslyckad kopiering inte raderar den lokala filen och inte lurar `trap cleanup`?
   - Räcker `set -euo pipefail` för att ett fel i kopieringen fäller cron-körningen med exitkod skild från noll?
2. **Vad som ligger på servern idag.**
   - Finns något i repot (compose, README, HANDOFF) som redan pekar ut en plats utanför servern för backuper, eller är det öppet? README säger "Kvar att besluta" – stämmer det fortfarande?
   - Innehåller `.env.prod.example` något fält för en sådan plats?
3. **Larmet.**
   - Finns någon extern övervakning av `/health` i dag (dokumenterad i HANDOFF eller ARCHITECTURE)? Om ja: kan samma tjänst läsa ett backupmärke?
   - Om nej: vad är den minsta vägen – en fil som `/health` läser och rapporterar som `backup_age`, eller en helt fristående kontroll? Vilka risker har vardera med att `/health` då avslöjar driftstatus utåt?
4. **Teståterställningen.**
   - Finns ett skript eller en dokumenterad rutin för återställning till tom databas i dag utöver stycket i README?
   - Vilka tabeller är rimliga att räkna rader i för att se att dumpen är hel? Kontrollera mot `Hemordna.Infrastructure/Migrations`, inte ur minnet.
5. **Hetzners egen backup.**
   - README beskriver överlappet. Täcker serverbackupen fallet att kontot stängs av, eller försvinner den då tillsammans med servern? Kontrollera mot källan, inte ur minnet.
6. **Integriteten.**
   - Bryter kopian utanför servern mot något i SECURITY.md eller CLAUDE.md?
   - Hamnar något okrypterat eller något personnamn i loggar, märkesfil eller larmtext?
7. **Bedömningen.**
   - Är förslaget värt att lägga i en uppgift, helt, delvis eller inte alls?
   - Vilka prov skulle visa att det fungerar? Föreslå rader till provavsnittet. Minst: en körning där kopieringen tvingas misslyckas ska lämna den lokala filen kvar och ge exitkod skild från noll.

### Svar

(Code skriver här.)

**Väntar:** Björns beslut om plats utanför servern och larmväg, så att en uppgift kan skrivas.
