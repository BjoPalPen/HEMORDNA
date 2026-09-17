# Säkerhet och atomiska rollbyten

Beslut 2026-09-17 efter repogenomgång.

- Identity låser lösenordsinloggningen i 15 minuter efter fem misslyckade försök.
  En lyckad inloggning nollställer räknaren. Okänd adress, fel lösenord och låst konto
  ger samma HTTP 401.
- Registrering, lösenordsinloggning, lösenordsåterställning, lösenordsbyte och anonym
  passkey-inloggning delar en gräns på 60 anrop per minut och klient-IP. Överskridande
  ger HTTP 429 med Retry-After. Gränsen gäller per API-process; produktionen kör en instans.
  AuthRateLimit:PermitLimit kan konfigureras. E2E-fixturen höjer gränsen för sin testsession;
  separata starttester verifierar själva begränsningen.
- Klient-IP och HTTPS-schema hämtas bara från betrodd proxy. Produktionsstacken anger
  bowling-edge-nätet via REVERSE_PROXY_NETWORK (verifierat 172.19.0.0/16).
  Kontrollera nätet när infrastrukturen ändras. API-porten exponeras inte på värden.
- Resend:ApiKey krävs utanför Development. Lokal e-post lagras i DevEmailOutbox;
  mejlinnehåll och återställningstoken skrivs aldrig till loggen.
- JWT bär Identitys säkerhetsstämpel, som jämförs med databasen vid autentisering.
  Lösenordsbyte och lösenordsåterställning gör äldre token ogiltiga. Lösenordsbyte
  returnerar en ny token till den aktuella klienten. Vid första deployen behöver
  befintliga sessioner logga in igen eftersom äldre token saknar stämpeln.
- SignalR stänger anslutningen när token löper ut. Klienten hämtar aktuell token vid
  återanslutning. En redan etablerad WebSocket autentiseras inte om vid varje push;
  pushen innehåller bara en ändringssignal och all data hämtas via behörighetskontrollerad REST.
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
