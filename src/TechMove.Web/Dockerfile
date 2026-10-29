# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# TechMove MVC frontend (glms-frontend-web) - multi-stage build
# Build from the repository root:  docker build -f src/TechMove.Web/Dockerfile .
# ---------------------------------------------------------------------------

# ---- Stage 1: build & publish with the full .NET SDK ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer), then copy the rest of the source.
COPY src/TechMove.Web/TechMove.Web.csproj src/TechMove.Web/
RUN dotnet restore src/TechMove.Web/TechMove.Web.csproj

COPY src/TechMove.Web/ src/TechMove.Web/
RUN dotnet publish src/TechMove.Web/TechMove.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---- Stage 2: runtime-only image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Api__BaseUrl is supplied by docker-compose (http://glms-backend-api:8080/).
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

# Run as the non-root 'app' user that ships with the .NET images.
USER $APP_UID

ENTRYPOINT ["dotnet", "TechMove.Web.dll"]
