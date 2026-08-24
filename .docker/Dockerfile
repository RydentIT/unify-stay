# UnifyStay API.
# Build context is the REPOSITORY ROOT, not this folder:
#   docker build -f .docker/Dockerfile -t unify-api .
# The Infrastructure project embeds database/sql/*.sql as resources, so that folder has to be
# present at build time even though it lives outside src/backend.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against project files only, so a source-only change reuses the cached restore layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/backend/Unify.Domain/Unify.Domain.csproj                 src/backend/Unify.Domain/
COPY src/backend/Unify.Application/Unify.Application.csproj       src/backend/Unify.Application/
COPY src/backend/Unify.Infrastructure/Unify.Infrastructure.csproj src/backend/Unify.Infrastructure/
COPY src/backend/Unify.Api/Unify.Api.csproj                       src/backend/Unify.Api/
RUN dotnet restore src/backend/Unify.Api/Unify.Api.csproj

COPY database/ database/
COPY src/backend/ src/backend/

RUN dotnet publish src/backend/Unify.Api/Unify.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# The aspnet images ship a non-root 'app' user (uid 1654). Run as it.
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

EXPOSE 8080

COPY --from=build --chown=app:app /app/publish .

# No secrets are baked in. Connection string, JWT signing key and everything else arrive as
# environment variables at run time - see .env.example.
ENTRYPOINT ["dotnet", "Unify.Api.dll"]
