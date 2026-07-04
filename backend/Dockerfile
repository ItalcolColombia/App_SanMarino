# =========================
# Etapa 1: Runtime base (optimizado)
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
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
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Crear usuario no-root para seguridad
RUN groupadd -r appuser && useradd -r -g appuser -u 1001 appuser && \
    chown -R appuser:appuser /app

EXPOSE 5002

# Healthcheck optimizado
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:5002/health || exit 1

# =========================
# Etapa 2: Restore (separado para mejor cache)
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Cert corporativo opcional — solo activo en Windows con SSL inspection (ej. Kaspersky)
# En Mac: secret no provisto → bloque sin efecto. En Windows: cert inyectado antes del restore.
RUN --mount=type=secret,id=corporate_ca,required=false \
    if [ -f /run/secrets/corporate_ca ] && [ -s /run/secrets/corporate_ca ]; then \
      cp /run/secrets/corporate_ca /usr/local/share/ca-certificates/corporate-ca.crt && \
      update-ca-certificates; \
    fi

# Copiar solución y archivos .csproj para optimizar cache de restore
COPY ./ZooSanMarino.sln ./
COPY ./src/ZooSanMarino.API/ZooSanMarino.API.csproj ./src/ZooSanMarino.API/
COPY ./src/ZooSanMarino.Application/ZooSanMarino.Application.csproj ./src/ZooSanMarino.Application/
COPY ./src/ZooSanMarino.Domain/ZooSanMarino.Domain.csproj ./src/ZooSanMarino.Domain/
COPY ./src/ZooSanMarino.Infrastructure/ZooSanMarino.Infrastructure.csproj ./src/ZooSanMarino.Infrastructure/

# Restore optimizado: restaurar solo el proyecto API que automáticamente restaurará sus dependencias
# Esto es más eficiente que restaurar cada proyecto individualmente
RUN dotnet restore ./src/ZooSanMarino.API/ZooSanMarino.API.csproj \
    --nologo \
    --verbosity quiet \
    --runtime linux-x64 \
    --source https://api.nuget.org/v3/index.json

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
