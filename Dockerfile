# --- Build API ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-api
WORKDIR /src
# Layer-caching: restore against just the .csproj files first, so a source-only change does
# not invalidate the restore layer - see Hemordna.Client's build stage for the same pattern.
COPY src/Hemordna.Domain/Hemordna.Domain.csproj src/Hemordna.Domain/
COPY src/Hemordna.Application/Hemordna.Application.csproj src/Hemordna.Application/
COPY src/Hemordna.Infrastructure/Hemordna.Infrastructure.csproj src/Hemordna.Infrastructure/
COPY src/Hemordna.Api/Hemordna.Api.csproj src/Hemordna.Api/
RUN dotnet restore src/Hemordna.Api/Hemordna.Api.csproj
COPY . .
RUN dotnet publish src/Hemordna.Api/Hemordna.Api.csproj -c Release -o /app/api --no-restore

# --- Build Client ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-client
WORKDIR /src
COPY src/Hemordna.Domain/Hemordna.Domain.csproj src/Hemordna.Domain/
COPY src/Hemordna.Client/Hemordna.Client.csproj src/Hemordna.Client/
RUN dotnet restore src/Hemordna.Client/Hemordna.Client.csproj
COPY . .
RUN dotnet publish src/Hemordna.Client/Hemordna.Client.csproj -c Release -o /app/client --no-restore

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app/api
COPY --from=build-api /app/api .
# The API serves the published Blazor client from its own wwwroot - see Program.cs's
# UseStaticFiles/MapFallbackToFile - so Caddy only needs one upstream per domain.
COPY --from=build-client /app/client/wwwroot ./wwwroot

# /keys backs the Data Protection key ring (see Program.cs's DataProtection:KeyPath) - created
# and owned by 'app' here so the named volume mounted over it in docker-compose.prod.yml
# inherits that ownership instead of Docker's default root:root on first use.
RUN mkdir -p /keys

# Non-root, matching the aspnet image's predefined 'app' user (UID 1654).
RUN chown -R app:app /app /keys
USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Hemordna.Api.dll"]
