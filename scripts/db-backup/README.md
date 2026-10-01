# Krypterad säkerhetskopiering av produktionsdatabasen

`backup-db.sh` tar en `pg_dump` av produktionsdatabasen, komprimerar den och krypterar den
med en publik GPG-nyckel. Kedjan är en enda pipe – ingenting okrypterat rör disken.

## Nyckeln

Servern har **bara den publika nyckeln**. Den kan skriva en backup men aldrig läsa en.
Den privata nyckeln finns hos Björn, på hans egen maskin, och ska aldrig läggas på servern –
det är hela poängen: den som tar sig in på servern kommer åt den levande databasen, men inte
åt historiken i backuperna.

```
Hemordna Backup <backup@hemordna.se>
BD4C890C0D27E84888D541685C204E9D2F9E49CE
```

**Tappas den privata nyckeln är varje backup obrukbar.** Den bör exporteras till ett
lösenordshanterarvalv:

```bash
gpg --export-secret-keys --armor backup@hemordna.se
```

## Installation på servern

```bash
# publik nyckel (en gång)
gpg --export --armor backup@hemordna.se | ssh deploy@<server> 'gpg --import'

# skriptet
scp scripts/db-backup/backup-db.sh deploy@<server>:~/backup-db.sh
ssh deploy@<server> 'chmod +x ~/backup-db.sh'

# dagligen 03:15 UTC
ssh deploy@<server> 'crontab -l 2>/dev/null; echo "15 3 * * * /home/deploy/backup-db.sh >> /home/deploy/backups/backup.log 2>&1"' | ssh deploy@<server> 'crontab -'
```

## Återställning

Måste göras på en maskin som har den privata nyckeln.

```bash
scp deploy@<server>:~/backups/hemordna-<tidpunkt>.sql.gz.gpg .
gpg --decrypt hemordna-<tidpunkt>.sql.gz.gpg | gunzip | psql -U hemordna -d <måldatabas>
```

Återställ alltid till en **tom** databas, aldrig rakt ovanpå en befintlig – dumpen innehåller
`CREATE`-satser och inget `DROP`.

## Förhållandet till Hetzners egen backup

Servern backas också upp av Hetzner. De två överlappar mindre än man tror, och ersätter inte
varandra:

| | Hetzners serverbackup | `backup-db.sh` |
|---|---|---|
| Förlorad disk eller hel server | **Skyddat** | Nej – ligger på samma maskin |
| Felaktig radering, trasig migrering | Återställer *hela* maskinen | **Kirurgiskt** – en databas, en tidpunkt |
| Konsistens | Ögonblicksbild av en körande databas, återställs via WAL | **Transaktionskonsistent dump** |
| Läsbar för leverantören | Ja | **Nej** – krypterad för en nyckel servern inte har |

Skillnaden som spelar mest roll i praktiken är den andra raden. Går en migrering fel på
morgonen är en serveråterställning ett slägga: allt annat som hänt sedan ögonblicksbilden
försvinner också. En dump går att läsa in i en tom databas, jämföra mot, och plocka ur – utan
att röra det som körs.

Den tredje raden är den som sällan nämns: en ögonblicksbild av en körande databas är
krasch-konsistent, inte transaktionskonsistent. Postgres klarar normalt det via WAL, men
"brukar gå bra" är en svagare garanti än en dump som per definition är tagen i ett konsistent
läge.

Kvar att besluta: om dumparna också ska kopieras utanför maskinen. Med Hetzners serverbackup
på plats är det inte längre akut – det handlar om att kunna återställa en enskild databas även
om hela Hetzner-kontot vore otillgängligt.

## Verifiering

En backup som ingen återställt är en förhoppning, inte ett skydd. Återställningen är
genomförd och verifierad mot radantal den 2026-10-01; gör om det när något i kedjan ändras.
