# =========================
# Etapa 1: Runtime base
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Puerto interno de la API
ENV ASPNETCORE_URLS=http://+:5002
ENV DOTNET_EnableDiagnostics=0
# (Forwarded headers útil si luego pones ALB/NGINX delante)
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Certificados raíz (para HTTPS hacia RDS, etc.)
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 5002

# Healthcheck a nivel de imagen (Docker/Compose/ECS lo utiliza si no se sobreescribe)
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -fsS http://localhost:5002/health || exit 1

# =========================
# Etapa 2: Build + Publish
# =========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiamos solución y proyectos (ajusta si tu layout cambia)
COPY ./ZooSanMarino.sln ./
COPY ./src ./src
COPY ./tests ./tests

# Restore
RUN dotnet restore "ZooSanMarino.sln" --nologo

# Publish API
WORKDIR /src/src/ZooSanMarino.API
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# =========================
# Etapa 3: Final runtime
# =========================
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Entorno por defecto en contenedor
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ZooSanMarino.API.dll"]
