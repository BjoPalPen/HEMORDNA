# Säkerhet och atomiska rollbyten

Beslut 2026-09-17 efter repogenomgång.

- Identity låser lösenordsinloggningen i 15 minuter efter fem misslyckade försök.
  En lyckad inloggning nollställer räknaren. Okänd adress, fel lösenord och låst konto
  ger samma HTTP 401.
- Registrering, lösenordsinloggning, lösenordsåterställning, lösenordsbyte, token-
  förnyelse, utloggning och anonym passkey-inloggning delar en gräns på 60 anrop per
  minut och klient-IP. Överskridande ger HTTP 429 med Retry-After. Gränsen gäller per
  API-process; produktionen kör en instans. AuthRateLimit:PermitLimit kan konfigureras.
  E2E-fixturen höjer gränsen för sin testsession; separata starttester verifierar själva
  begränsningen.
- Klient-IP och HTTPS-schema hämtas bara från betrodd proxy. Produktionsstacken anger
  bowling-edge-nätet via REVERSE_PROXY_NETWORK (verifierat 172.19.0.0/16).
  Kontrollera nätet när infrastrukturen ändras. API-porten exponeras inte på värden.
- Resend:ApiKey krävs utanför Development. Lokal e-post lagras i DevEmailOutbox;
  mejlinnehåll och återställningstoken skrivs aldrig till loggen.
- JWT bär Identitys säkerhetsstämpel, som jämförs med databasen vid autentisering.
  Lösenordsbyte och lösenordsåterställning gör äldre access-token ogiltiga. Vid första
  deployen av den kontrollen behövde befintliga sessioner logga in igen eftersom äldre
  token saknade stämpeln.
- **Refresh-token med rotation (2026-09-26), se docs/ARCHITECTURE.md "Beslut: Refresh-
  token med rotation" för hela resonemanget.** Access-token är kort (30 minuter) och
  ligger enbart i minnet i klienten, aldrig i `localStorage`. En separat refresh-token
  (60 dagar, förnyad vid varje rotation, `localStorage`-nyckel `hemordna.refresh`) hämtar
  tyst nya access-token via `POST /api/auth/refresh`; endast en SHA-256-hash av den
  lagras. Varje användning roterar token och markerar den gamla förbrukad; en redan
  förbrukad eller återkallad token återkallar hela kedjan (återanvändningsdetektering).
  Lösenordsbyte återkallar alla kedjor för användaren men utfärdar omedelbart en ny till
  den anropande enheten; lösenordsåterställning återkallar utan att utfärda en ny - se
  ARCHITECTURE.md för varför de två flödena medvetet skiljer sig åt. Klientens egen
  single-flight-spärr förhindrar att flera samtidiga 401-svar blir flera samtidiga
  `/refresh`-anrop med samma token, vilket annars hade utlöst återanvändningsdetektering
  och loggat ut användaren på riktigt. **Engångsutloggning vid den här driftsättningen:**
  ingen befintlig användare har en refresh-token än, och access-token läses inte längre
  från disk - alla befintliga sessioner loggas ut precis en gång vid nästa appstart.
- SignalR stänger anslutningen när access-token löper ut. Klienten säkerställer en giltig
  (vid behov nyss tyst förnyad) token innan varje anslutningsförsök, inklusive SignalRs
  egna automatiska återanslutning - verifierat, inte antaget: utan detta hade en
  återanslutning efter att access-token gått ut presenterat samma utgångna token om och
  om igen. En redan etablerad WebSocket autentiseras inte om vid varje push; pushen
  innehåller bara en ändringssignal och all data hämtas via behörighetskontrollerad REST.
- Rollförval sparar roll, veckobudget och orktak i ett anrop och en SaveChanges-operation.
  Ogiltig kapacitet avvisar hela ändringen. Befintliga rollanrop utan kapacitet fortsätter
  fungera och bevarar egna inställningar.

Ingen datamigration behövs. Verifiering omfattar domän- och applikationstester,
HTTP-tester för säkerhetsbeteenden, produktionsstart och rate limiting samt UI-tester
för lösenordsbyte, rollbyte och realtid.

Efter deploy: `dotnet run --project scripts/Smoke -- https://app.hemordna.se`.
Kontrollen läser health och provar klientens inloggning med en okänd adress på mobil
och desktop. Inga konton eller hushåll skapas. Kräver installerad Playwright Chromium
(samma version som E2E-testerna).

## Planeringsgränser och befintliga felvärden

IMPLEMENTED: Uppgiftslängd är 0–1 440 minuter. Noll är fortsatt ett avsiktligt
val; taket motsvarar ett helt dygns arbete och gäller både skapande och ändring.
Tidskrediternas netto summeras som long, begränsas till medlemmens befintliga tak
och konverteras därefter till int. Äldre extrema kreditposter behöver inte ändras
för att dagsplaneringen ska fungera.

Nya occurrences och begärda planeringsdatum får ligga högst fem kalenderår efter
den uttryckligen tillförda referensdagen. Domänen läser ingen systemklocka.
Kalendergränserna lämnar utrymme för 60 dagars bakåtblick och fortsatt generering.
Flytt av redan sparat arbete kontrollerar kalendergränserna, men inte åldern på
uppgiften: gammalt kvarlämnat arbete måste fortfarande kunna flyttas till nutid.

En occurrence som bokades efter sin skapandedag får inte flytta markören för
återkommande generering. Redan täckta originaldatum räknas i alla statusar;
en fortfarande planerad uppgift som flyttats till datumet täcker också den platsen.
Det bevarar framtidsbokningar utan att hoppa över mellanliggande uppgifter eller
återskapa slutförda/hoppade bokningar. Redan täckta datum förbrukar inte gränsen
på 366 nya eller pausade platser per körning; genomsökningen har också ett eget tak.

Ingen schemamigration eller automatisk radering/omskrivning av historik behövs.
Äldre maxdatum ignoreras som genereringsmarkörer och blockerar inte dagens planer.
Om sådana poster ska korrigeras i databasen: säkerhetskopiera först, identifiera dem
per hushåll och bekräfta avsett datum med hushållet. Samma försiktighet gäller äldre
orimliga uppgiftslängder; ändring av en definition skriver inte om dess snapshots.
