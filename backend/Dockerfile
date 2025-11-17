# =========================
# Etapa 1: Runtime base (optimizado)
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Variables de entorno optimizadas
ENV ASPNETCORE_URLS=http://+:5002
ENV DOTNET_EnableDiagnostics=0
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Instalar dependencias mínimas y limpiar en una sola capa
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        libicu74 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Crear usuario no-root para seguridad
RUN groupadd -r appuser && useradd -r -g appuser -u 1000 appuser && \
    chown -R appuser:appuser /app

EXPOSE 5002

# Healthcheck optimizado
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:5002/health || exit 1

# =========================
# Etapa 2: Restore (separado para mejor cache)
# =========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copiar solución y archivos .csproj para optimizar cache de restore
COPY ./ZooSanMarino.sln ./
COPY ./src/ZooSanMarino.API/ZooSanMarino.API.csproj ./src/ZooSanMarino.API/
COPY ./src/ZooSanMarino.Application/ZooSanMarino.Application.csproj ./src/ZooSanMarino.Application/
COPY ./src/ZooSanMarino.Domain/ZooSanMarino.Domain.csproj ./src/ZooSanMarino.Domain/
COPY ./src/ZooSanMarino.Infrastructure/ZooSanMarino.Infrastructure.csproj ./src/ZooSanMarino.Infrastructure/

# Restore con cache optimizado
RUN dotnet restore "ZooSanMarino.sln" \
    --nologo \
    --verbosity quiet \
    --runtime linux-x64

# =========================
# Etapa 3: Build + Publish
# =========================
FROM restore AS build
WORKDIR /src

# Copiar código fuente (solo src, sin tests para reducir tamaño)
COPY ./src ./src

# Build y publish optimizado
WORKDIR /src/src/ZooSanMarino.API
RUN dotnet publish \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-x64 \
    --self-contained false \
    /p:UseAppHost=false \
    /p:PublishTrimmed=false \
    /p:EnableCompressionInSingleFile=true

# =========================
# Etapa 4: Final runtime (optimizado)
# =========================
FROM base AS final
WORKDIR /app

# Copiar solo artefactos publicados
COPY --from=build --chown=appuser:appuser /app/publish ./

# Cambiar a usuario no-root
USER appuser

# Variables de entorno finales
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ZooSanMarino.API.dll"]
