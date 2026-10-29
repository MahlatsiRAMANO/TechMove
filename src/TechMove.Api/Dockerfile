# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# TechMove API (glms-backend-api) - multi-stage build
# Build from the repository root:  docker build -f src/TechMove.Api/Dockerfile .
# ---------------------------------------------------------------------------

# ---- Stage 1: build & publish with the full .NET SDK ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first so the NuGet restore layer is cached
# and is re-used until the package references change.
COPY src/TechMove.Api/TechMove.Api.csproj src/TechMove.Api/
RUN dotnet restore src/TechMove.Api/TechMove.Api.csproj

# Now copy the source and publish a Release build.
COPY src/TechMove.Api/ src/TechMove.Api/
RUN dotnet publish src/TechMove.Api/TechMove.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---- Stage 2: small runtime-only image (no SDK, no source code) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    FileStorage__RootPath=/app/App_Data/agreements

EXPOSE 8080

COPY --from=build /app/publish .

# Folder for uploaded signed agreements (mounted as a volume in docker-compose),
# owned by the non-root 'app' user that ships with the .NET images.
RUN mkdir -p /app/App_Data/agreements && chown -R "$APP_UID" /app/App_Data
USER $APP_UID

ENTRYPOINT ["dotnet", "TechMove.Api.dll"]
