# HEMORDNA

Hemmets organisationsapp – en svensk PWA för hushållsplanering.

> Hemordna ska hjälpa en eller flera personer i ett hushåll att veta vad som behöver göras,
> vem som ansvarar och vad som är rimligt att göra idag.

- Produktbeskrivning: [docs/PRODUCT.md](docs/PRODUCT.md)
- Arkitektur och beslutsstatus: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Arbetsregler för Claude Code: [CLAUDE.md](CLAUDE.md)

## Kom igång

Projektet körs i devcontainer/Codespaces. PostgreSQL startar med miljön via
`.devcontainer/docker-compose.yml` och connection string sätts som
`ConnectionStrings__Hemordna`.

API:t kräver en JWT-signeringsnyckel. Den har medvetet ingen default – API:t vägrar starta
utan den, och den ska aldrig checkas in:

```bash
export Jwt__SigningKey="$(openssl rand -base64 48)"
```

Applicera migrationer och starta:

```bash
dotnet ef database update \
  --project src/Hemordna.Infrastructure \
  --startup-project src/Hemordna.Api

dotnet run --project src/Hemordna.Api
```

## Bygg och testa

```bash
dotnet build Hemordna.slnx
dotnet test Hemordna.slnx
```

## Struktur

```text
src/Hemordna.Domain          entiteter, value objects, domänregler
src/Hemordna.Application     use cases, DailyPlanner
src/Hemordna.Infrastructure  EF Core, PostgreSQL, Identity
src/Hemordna.Api             Minimal APIs, JWT, DTO:er
src/Hemordna.Client          Blazor WebAssembly PWA
```
