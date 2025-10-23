# ⚙️ Configuración de Entorno - ZooSanMarino Backend

## 🎯 Descripción General

Este documento describe la configuración completa del entorno para el backend de ZooSanMarino, incluyendo variables de entorno, configuraciones de base de datos, autenticación JWT, logging y configuraciones específicas para diferentes entornos (Development, Staging, Production).

## 📋 Variables de Entorno

### Archivo .env Principal

```env
# ===========================================
# CONFIGURACIÓN DE BASE DE DATOS
# ===========================================
ZOO_CONN=Host=localhost;Port=5432;Database=zoosanmarino_dev;Username=postgres;Password=tu_password_seguro

# ===========================================
# CONFIGURACIÓN JWT
# ===========================================
JWT_SECRET=tu_clave_secreta_muy_larga_y_segura_aqui_minimo_32_caracteres_para_mayor_seguridad
JWT_ISSUER=ZooSanMarino
JWT_AUDIENCE=ZooSanMarino.API
JWT_EXPIRATION_HOURS=24

# ===========================================
# CONFIGURACIÓN DE APLICACIÓN
# ===========================================
ASPNETCORE_ENVIRONMENT=Development
PORT=5002
API_VERSION=1.0.0

# ===========================================
# CONFIGURACIÓN DE LOGGING
# ===========================================
LOG_LEVEL=Information
LOG_TO_FILE=true
LOG_FILE_PATH=logs/zoosanmarino.log

# ===========================================
# CONFIGURACIÓN DE EXCEL/EPPLUS
# ===========================================
EPPLUS_LICENSE_CONTEXT=NonCommercial
MAX_EXCEL_FILE_SIZE_MB=10

# ===========================================
# CONFIGURACIÓN DE CORS
# ===========================================
CORS_ORIGINS=http://localhost:4200,http://localhost:3000
CORS_ALLOW_CREDENTIALS=true

# ===========================================
# CONFIGURACIÓN DE RATE LIMITING
# ===========================================
RATE_LIMIT_REQUESTS_PER_MINUTE=100
RATE_LIMIT_BURST_SIZE=20

# ===========================================
# CONFIGURACIÓN DE CACHE
# ===========================================
CACHE_ENABLED=true
CACHE_EXPIRATION_MINUTES=30

# ===========================================
# CONFIGURACIÓN DE ARCHIVOS
# ===========================================
UPLOAD_PATH=uploads
MAX_UPLOAD_SIZE_MB=50
ALLOWED_FILE_EXTENSIONS=.xlsx,.xls,.csv
```

### Variables por Entorno

#### Development (.env.development)
```env
ASPNETCORE_ENVIRONMENT=Development
ZOO_CONN=Host=localhost;Port=5432;Database=zoosanmarino_dev;Username=postgres;Password=dev_password
LOG_LEVEL=Debug
CORS_ORIGINS=http://localhost:4200
SWAGGER_ENABLED=true
DETAILED_ERRORS=true
```

#### Staging (.env.staging)
```env
ASPNETCORE_ENVIRONMENT=Staging
ZOO_CONN=Host=staging-db.company.com;Port=5432;Database=zoosanmarino_staging;Username=zoo_user;Password=${DB_PASSWORD}
LOG_LEVEL=Information
CORS_ORIGINS=https://staging.zoosanmarino.com
SWAGGER_ENABLED=true
DETAILED_ERRORS=false
```

#### Production (.env.production)
```env
ASPNETCORE_ENVIRONMENT=Production
ZOO_CONN=Host=prod-db.company.com;Port=5432;Database=zoosanmarino_prod;Username=zoo_prod;Password=${DB_PASSWORD}
LOG_LEVEL=Warning
CORS_ORIGINS=https://app.zoosanmarino.com
SWAGGER_ENABLED=false
DETAILED_ERRORS=false
HTTPS_REDIRECT=true
```

## 🔧 Configuración en appsettings.json

### appsettings.json (Base)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "ZooSanMarinoContext": ""
  },
  "JwtSettings": {
    "Secret": "",
    "Issuer": "ZooSanMarino",
    "Audience": "ZooSanMarino.API",
    "ExpirationHours": 24
  },
  "CorsSettings": {
    "AllowedOrigins": [],
    "AllowCredentials": true,
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["*"]
  },
  "SwaggerSettings": {
    "Title": "ZooSanMarino API",
    "Version": "v1",
    "Description": "API para gestión de datos avícolas",
    "ContactName": "Equipo de Desarrollo",
    "ContactEmail": "dev@zoosanmarino.com"
  },
  "FileUploadSettings": {
    "MaxFileSizeMB": 10,
    "AllowedExtensions": [".xlsx", ".xls", ".csv"],
    "UploadPath": "uploads",
    "TempPath": "temp"
  },
  "ExcelSettings": {
    "LicenseContext": "NonCommercial",
    "MaxRowsPerFile": 10000,
    "DefaultTimeout": 30
  },
  "RateLimitSettings": {
    "EnableRateLimit": true,
    "RequestsPerMinute": 100,
    "BurstSize": 20,
    "ClientIdHeader": "X-Client-Id"
  }
}
```

### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "SwaggerSettings": {
    "Enabled": true,
    "IncludeXmlComments": true
  },
  "DetailedErrors": true,
  "DeveloperExceptionPage": true
}
```

### appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Error",
      "Microsoft.EntityFrameworkCore": "Error"
    }
  },
  "SwaggerSettings": {
    "Enabled": false
  },
  "DetailedErrors": false,
  "UseHttpsRedirection": true,
  "UseHsts": true
}
```

## 🔐 Configuración de Seguridad

### JWT Configuration
```csharp
// Program.cs - Configuración JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!)
            ),
            ClockSkew = TimeSpan.Zero
        };
    });
```

### CORS Configuration
```csharp
// Program.cs - Configuración CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();
            
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

### HTTPS Configuration
```csharp
// Program.cs - Configuración HTTPS (Production)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

## 🗄️ Configuración de Base de Datos

### Connection String Patterns

#### Local Development
```
Host=localhost;Port=5432;Database=zoosanmarino_dev;Username=postgres;Password=dev_password;Include Error Detail=true
```

#### Docker Container
```
Host=zoosanmarino-postgres;Port=5432;Database=zoosanmarino;Username=zoo_user;Password=secure_password;Pooling=true;MinPoolSize=5;MaxPoolSize=20
```

#### Azure PostgreSQL
```
Host=zoosanmarino.postgres.database.azure.com;Port=5432;Database=zoosanmarino;Username=zoo_admin@zoosanmarino;Password=${AZURE_DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true
```

#### AWS RDS
```
Host=zoosanmarino.cluster-xyz.us-east-1.rds.amazonaws.com;Port=5432;Database=zoosanmarino;Username=zoo_user;Password=${AWS_DB_PASSWORD};SSL Mode=Require
```

### Entity Framework Configuration
```csharp
// Program.cs - Configuración EF Core
builder.Services.AddDbContext<ZooSanMarinoContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ZooSanMarinoContext")
        ?? builder.Configuration["ZOO_CONN"]
        ?? Environment.GetEnvironmentVariable("ZOO_CONN");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Connection string not configured");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null
        );
        npgsqlOptions.CommandTimeout(30);
    });

    // Configuraciones adicionales por entorno
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});
```

## 📝 Configuración de Logging

### Serilog Configuration
```csharp
// Program.cs - Configuración de Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        );

    // Logging a archivo en producción
    if (context.HostingEnvironment.IsProduction())
    {
        configuration.WriteTo.File(
            path: "logs/zoosanmarino-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 10_000_000,
            rollOnFileSizeLimit: true
        );
    }

    // Logging a base de datos para errores críticos
    if (!context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.PostgreSQL(
            connectionString: context.Configuration.GetConnectionString("ZooSanMarinoContext"),
            tableName: "logs",
            restrictedToMinimumLevel: LogEventLevel.Error
        );
    }
});
```

### Structured Logging Examples
```csharp
// En servicios
public class LiquidacionTecnicaService
{
    private readonly ILogger<LiquidacionTecnicaService> _logger;

    public async Task<LiquidacionTecnicaDto> CalcularLiquidacionAsync(LiquidacionTecnicaRequest request)
    {
        _logger.LogInformation("Iniciando cálculo de liquidación para lote {LoteId}", request.LoteId);
        
        try
        {
            var resultado = await PerformCalculation(request);
            
            _logger.LogInformation(
                "Liquidación calculada exitosamente para lote {LoteId}. Retiro total: {RetiroTotal}%",
                request.LoteId,
                resultado.PorcentajeRetiroTotalGeneral
            );
            
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error al calcular liquidación para lote {LoteId}", 
                request.LoteId
            );
            throw;
        }
    }
}
```

## 🚀 Configuración de Performance

### Response Caching
```csharp
// Program.cs - Configuración de Cache
builder.Services.AddResponseCaching();
builder.Services.AddMemoryCache();

// Configuración personalizada
builder.Services.Configure<CacheOptions>(options =>
{
    options.DefaultExpiration = TimeSpan.FromMinutes(30);
    options.MaxSize = 100_000_000; // 100 MB
});
```

### Response Compression
```csharp
// Program.cs - Configuración de Compresión
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
```

### Rate Limiting
```csharp
// Program.cs - Configuración de Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ApiPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 20;
    });
});
```

## 🐳 Configuración Docker

### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/ZooSanMarino.API/ZooSanMarino.API.csproj", "src/ZooSanMarino.API/"]
COPY ["src/ZooSanMarino.Infrastructure/ZooSanMarino.Infrastructure.csproj", "src/ZooSanMarino.Infrastructure/"]
COPY ["src/ZooSanMarino.Application/ZooSanMarino.Application.csproj", "src/ZooSanMarino.Application/"]
COPY ["src/ZooSanMarino.Domain/ZooSanMarino.Domain.csproj", "src/ZooSanMarino.Domain/"]

RUN dotnet restore "src/ZooSanMarino.API/ZooSanMarino.API.csproj"
COPY . .
WORKDIR "/src/src/ZooSanMarino.API"
RUN dotnet build "ZooSanMarino.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ZooSanMarino.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Crear directorios necesarios
RUN mkdir -p /app/logs /app/uploads /app/temp

# Variables de entorno por defecto
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=80

ENTRYPOINT ["dotnet", "ZooSanMarino.API.dll"]
```

### docker-compose.yml
```yaml
version: '3.8'

services:
  zoosanmarino-api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "5002:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ZOO_CONN=Host=zoosanmarino-postgres;Port=5432;Database=zoosanmarino;Username=zoo_user;Password=secure_password
      - JWT_SECRET=${JWT_SECRET}
    depends_on:
      - zoosanmarino-postgres
    volumes:
      - ./logs:/app/logs
      - ./uploads:/app/uploads
    networks:
      - zoosanmarino-network

  zoosanmarino-postgres:
    image: postgres:15
    environment:
      - POSTGRES_DB=zoosanmarino
      - POSTGRES_USER=zoo_user
      - POSTGRES_PASSWORD=secure_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/init.sql:/docker-entrypoint-initdb.d/init.sql
    networks:
      - zoosanmarino-network

volumes:
  postgres_data:

networks:
  zoosanmarino-network:
    driver: bridge
```

## 🔍 Health Checks

### Configuración de Health Checks
```csharp
// Program.cs - Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("ZooSanMarinoContext")!,
        name: "postgresql",
        tags: new[] { "database" }
    )
    .AddCheck<CustomHealthCheck>("custom-check")
    .AddMemoryHealthCheck("memory", tags: new[] { "memory" });

// Endpoint de health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
```

## 📊 Monitoreo y Métricas

### Application Insights (Azure)
```csharp
// Program.cs - Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});
```

### Custom Metrics
```csharp
// Ejemplo de métricas personalizadas
public class MetricsService
{
    private readonly IMetrics _metrics;
    private readonly Counter<int> _liquidacionesCalculadas;
    private readonly Histogram<double> _tiempoCalculoLiquidacion;

    public MetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ZooSanMarino.API");
        _liquidacionesCalculadas = meter.CreateCounter<int>("liquidaciones_calculadas_total");
        _tiempoCalculoLiquidacion = meter.CreateHistogram<double>("tiempo_calculo_liquidacion_segundos");
    }

    public void RecordLiquidacionCalculada(string loteId, double tiempoSegundos)
    {
        _liquidacionesCalculadas.Add(1, new("lote_id", loteId));
        _tiempoCalculoLiquidacion.Record(tiempoSegundos, new("lote_id", loteId));
    }
}
```

## 🔧 Scripts de Configuración

### Script de Inicialización (init.sh)
```bash
#!/bin/bash

# Script de inicialización para entorno de producción

echo "🚀 Iniciando configuración de ZooSanMarino Backend..."

# Verificar variables de entorno requeridas
required_vars=("ZOO_CONN" "JWT_SECRET")
for var in "${required_vars[@]}"; do
    if [ -z "${!var}" ]; then
        echo "❌ Error: Variable de entorno $var no está configurada"
        exit 1
    fi
done

# Crear directorios necesarios
mkdir -p logs uploads temp

# Configurar permisos
chmod 755 logs uploads temp

# Ejecutar migraciones de base de datos
echo "📊 Aplicando migraciones de base de datos..."
dotnet ef database update --no-build

# Verificar conexión a base de datos
echo "🔍 Verificando conexión a base de datos..."
dotnet run --no-build --urls="http://localhost:5000" &
API_PID=$!

sleep 10

# Health check
if curl -f http://localhost:5000/health; then
    echo "✅ API iniciada correctamente"
else
    echo "❌ Error: API no responde"
    kill $API_PID
    exit 1
fi

kill $API_PID
echo "🎉 Configuración completada exitosamente"
```

### Script de Backup (.env.backup.sh)
```bash
#!/bin/bash

# Script para backup de configuración

BACKUP_DIR="backups/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

# Backup de archivos de configuración
cp .env* "$BACKUP_DIR/" 2>/dev/null || true
cp appsettings*.json "$BACKUP_DIR/" 2>/dev/null || true

# Backup de base de datos
pg_dump "$ZOO_CONN" > "$BACKUP_DIR/database_backup.sql"

echo "✅ Backup creado en: $BACKUP_DIR"
```

---

**Última actualización:** Octubre 2024  
**Entornos soportados:** Development, Staging, Production  
**Plataformas:** Linux, Windows, macOS, Docker  
**Base de datos:** PostgreSQL 13+
