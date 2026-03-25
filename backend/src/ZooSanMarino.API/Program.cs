// file: backend/src/ZooSanMarino.API/Program.cs
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using EFCore.NamingConventions;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.Swagger;          // ISwaggerProvider (para /swagger/download)
using Swashbuckle.AspNetCore.SwaggerUI;       // Opciones UI

using ZooSanMarino.API.Extensions;
using ZooSanMarino.API.Infrastructure;
using ZooSanMarino.API.Configuration;
using ZooSanMarino.API.Middleware;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.Options;
using ZooSanMarino.Application.Validators;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Infrastructure.Providers;
using ZooSanMarino.Infrastructure.Services;
using IReporteTecnicoService = ZooSanMarino.Application.Interfaces.IReporteTecnicoService;
using ReporteTecnicoService = ZooSanMarino.Infrastructure.Services.ReporteTecnicoService;
using IReporteTecnicoProduccionService = ZooSanMarino.Application.Interfaces.IReporteTecnicoProduccionService;
using ReporteTecnicoProduccionService = ZooSanMarino.Infrastructure.Services.ReporteTecnicoProduccionService;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────
// 0) Cargar .env y shim ZOO_CONN
// ─────────────────────────────────────
static void LoadDotEnvIfExists(string path)
{
    if (!File.Exists(path)) return;
    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
        var idx = line.IndexOf('=');
        if (idx <= 0) continue;

        var key = line[..idx].Trim();
        var val = line[(idx + 1)..].Trim();

        if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
            val = val[1..^1];

        Environment.SetEnvironmentVariable(key, val);
    }
}

var envPaths = new[]
{
    Path.Combine(builder.Environment.ContentRootPath, ".env"),
    Path.Combine(builder.Environment.ContentRootPath, "..", ".env"),
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env"),
};
foreach (var p in envPaths) LoadDotEnvIfExists(Path.GetFullPath(p));

// Shim legacy
var legacyConn = Environment.GetEnvironmentVariable("ZOO_CONN");
if (!string.IsNullOrWhiteSpace(legacyConn))
{
    Environment.SetEnvironmentVariable("ConnectionStrings__ZooSanMarinoContext", legacyConn);
}

// ─────────────────────────────────────
// 1) Config
// ─────────────────────────────────────
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ─────────────────────────────────────
// 2) Puerto y límites de peticiones
// ─────────────────────────────────────
var port = builder.Configuration["PORT"] ?? "5002";
builder.WebHost.UseUrls($"http://+:{port}");

// Configurar límites de tamaño de request body a nivel de servidor
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB máximo
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32 KB para headers
    options.Limits.MaxRequestLineSize = 8 * 1024; // 8 KB para línea de request
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30); // Timeout de 30 segundos
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2); // Keep-alive de 2 minutos
});

// ─────────────────────────────────────
// 3) Conexión a BD (con fallbacks)
// En Development se prioriza appsettings.*.json para que la conexión local no sea sobrescrita por env vars (ZOO_CONN / ConnectionStrings__ZooSanMarinoContext).
// ─────────────────────────────────────
var conn =
    builder.Configuration.GetConnectionString("ZooSanMarinoContext")
    ?? builder.Configuration["ConnectionStrings:ZooSanMarinoContext"]
    ?? builder.Configuration["ZOO_CONN"]
    ?? Environment.GetEnvironmentVariable("ZOO_CONN");

if (builder.Environment.EnvironmentName == "Development")
{
    var devOnlyConfig = new ConfigurationBuilder()
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .Build();
    var devConn = devOnlyConfig.GetConnectionString("ZooSanMarinoContext")
        ?? devOnlyConfig["ConnectionStrings:ZooSanMarinoContext"];
    if (!string.IsNullOrWhiteSpace(devConn))
        conn = devConn;
}

if (string.IsNullOrWhiteSpace(conn))
    throw new InvalidOperationException("ConnectionStrings:ZooSanMarinoContext no está configurada (revisa .env y/o appsettings).");

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ─────────────────────────────────────
// 4) JWT
// ─────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSettings"));
var jwt = builder.Configuration.GetSection("JwtSettings").Get<JwtOptions>() ?? new JwtOptions();
jwt.EnsureValid();
builder.Services.AddSingleton(jwt);

// ─────────────────────────────────────
// 5) CORS (AllowedOrigins)
// ─────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCorsFromOrigins("AppCors", allowedOrigins);

// ─────────────────────────────────────
/* 6) DbContext */
// ─────────────────────────────────────
builder.Services.AddDbContext<ZooSanMarinoContext>(opts =>
    opts.UseSnakeCaseNamingConvention()
        .UseNpgsql(conn));

// ─────────────────────────────────────
// 7) Infra básica
// ─────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher<Login>, PasswordHasher<Login>>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ICompanyResolver, CompanyResolver>();
builder.Services.AddScoped<IUserPermissionService, UserPermissionService>();
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.ICompanyPaisValidator, ZooSanMarino.Infrastructure.Services.CompanyPaisValidator>();
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.ICompanyPaisService, ZooSanMarino.Infrastructure.Services.CompanyPaisService>();

// Cache en memoria para Rate Limiting y otros servicios
builder.Services.AddMemoryCache();

// HttpClient y servicio para reCAPTCHA
builder.Services.AddHttpClient<ZooSanMarino.Application.Interfaces.IRecaptchaService, 
    ZooSanMarino.Infrastructure.Services.RecaptchaService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10); // Timeout de 10 segundos para reCAPTCHA
    });

// Servicio de sanitización de inputs (prevención de inyección SQL)
builder.Services.AddSingleton<ZooSanMarino.API.Services.InputSanitizerService>();

// ─────────────────────────────────────
// 8) Servicios de aplicación/infra
// ─────────────────────────────────────
builder.Services.AddSingleton<EncryptionService>(); // Servicio de encriptación (Singleton porque es stateless y solo usa IConfiguration)
builder.Services.AddScoped<IEmailQueueService, EmailQueueService>(); // Servicio de cola de correos
builder.Services.AddScoped<IEmailService, EmailService>(); // Servicio de envío de correos (usa cola)
// Registrar procesador de cola de correos solo si está habilitado por configuración
var emailQueueEnabled = builder.Configuration.GetValue<bool?>("Email:Queue:Enabled") ?? false;
if (emailQueueEnabled)
{
    builder.Services.AddHostedService<ZooSanMarino.API.BackgroundServices.EmailQueueProcessorService>(); // Procesador de cola en segundo plano
}
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserFarmService, UserFarmService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ICompanyMenuService, CompanyMenuService>();
builder.Services.AddScoped<IFarmService, FarmService>();
builder.Services.AddScoped<INucleoService, NucleoService>();
builder.Services.AddScoped<IGalponService, GalponService>();
builder.Services.AddScoped<ILoteService, LoteService>();
builder.Services.AddScoped<ILotePosturaLevanteService, LotePosturaLevanteService>();
builder.Services.AddScoped<ILotePosturaProduccionService, LotePosturaProduccionService>();
builder.Services.AddScoped<ILoteFormDataService, LoteFormDataService>();
builder.Services.AddScoped<ILoteAveEngordeService, LoteAveEngordeService>();
builder.Services.AddScoped<ILoteReproductoraService, LoteReproductoraService>();
builder.Services.AddScoped<ILoteReproductoraFilterDataService, LoteReproductoraFilterDataService>();
builder.Services.AddScoped<ILoteReproductoraAveEngordeService, LoteReproductoraAveEngordeService>();
builder.Services.AddScoped<ILoteReproductoraAveEngordeFilterDataService, LoteReproductoraAveEngordeFilterDataService>();
builder.Services.AddScoped<ILoteProduccionFilterDataService, LoteProduccionFilterDataService>();
builder.Services.AddScoped<ILoteLevanteFilterDataService, LoteLevanteFilterDataService>();
builder.Services.AddScoped<IReporteTecnicoLevanteFilterDataService, ReporteTecnicoLevanteFilterDataService>();
builder.Services.AddScoped<ILoteGalponService, LoteGalponService>();
builder.Services.AddScoped<IRegionalService, RegionalService>();
builder.Services.AddScoped<IPaisService, PaisService>();
builder.Services.AddScoped<IDepartamentoService, DepartamentoService>();
builder.Services.AddScoped<IMunicipioService, MunicipioService>();
builder.Services.AddScoped<ILoteSeguimientoService, LoteSeguimientoService>();
builder.Services.AddScoped<ISeguimientoDiarioService, SeguimientoDiarioService>();
builder.Services.AddScoped<IMasterListService, MasterListService>();
// Sistema de Inventario de Aves (registrado antes para inyección en seguimientos)
builder.Services.AddScoped<IInventarioAvesService, InventarioAvesService>();
builder.Services.AddScoped<IHistorialInventarioService, HistorialInventarioService>();
builder.Services.AddScoped<IMovimientoAvesService, MovimientoAvesService>();
builder.Services.AddScoped<IMovimientoPolloEngordeService, MovimientoPolloEngordeService>();
builder.Services.AddScoped<IMovimientoPolloEngordeFilterDataService, MovimientoPolloEngordeFilterDataService>();
builder.Services.AddScoped<IInventarioGastoService, InventarioGastoService>();

builder.Services.AddScoped<ISeguimientoLoteLevanteService, SeguimientoLoteLevanteService>();
builder.Services.AddScoped<ISeguimientoAvesEngordeService, SeguimientoAvesEngordeService>();
builder.Services.AddScoped<ISeguimientoAvesEngordeFilterDataService, SeguimientoAvesEngordeFilterDataService>();
builder.Services.AddScoped<ISeguimientoDiarioLoteReproductoraService, SeguimientoDiarioLoteReproductoraService>();
builder.Services.AddScoped<ISeguimientoDiarioLoteReproductoraFilterDataService, SeguimientoDiarioLoteReproductoraFilterDataService>();
builder.Services.AddScoped<IProduccionLoteService, ProduccionLoteService>();
builder.Services.AddScoped<IProduccionDiariaService, ProduccionDiariaService>();
builder.Services.AddScoped<IProduccionService, ProduccionService>();
builder.Services.AddScoped<ISeguimientoProduccionService, SeguimientoProduccionService>();
builder.Services.AddScoped<ICatalogItemService, CatalogItemService>();
builder.Services.AddScoped<IFarmInventoryService, FarmInventoryService>();
// builder.Services.AddScoped<IEmailService, EmailService>(); // Temporalmente comentado para debug
// builder.Services.AddScoped<IConfigurationService, ConfigurationService>(); // Temporalmente comentado para debug

// Configuración segura de credenciales - temporalmente comentada para debug
// builder.Services.AddSecureConfiguration(builder.Configuration);
builder.Services.AddScoped<IFarmInventoryMovementService, FarmInventoryMovementService>();
builder.Services.AddScoped<IFarmInventoryReportService, FarmInventoryReportService>();
builder.Services.AddScoped<IInventarioGestionService, InventarioGestionService>();
builder.Services.AddScoped<IItemInventarioEcuadorService, ItemInventarioEcuadorService>();
builder.Services.AddScoped<IPermissionService, PermissionService>(); 

// ✅ Servicio orquestador único de roles/permissions/menús
builder.Services.AddScoped<IRoleCompositeService, RoleCompositeService>();

// Producción Avícola Raw
builder.Services.AddScoped<IProduccionAvicolaRawService, ProduccionAvicolaRawService>();

// Excel Import Service
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();

// Liquidación Técnica Service
builder.Services.AddScoped<ILiquidacionTecnicaService, LiquidacionTecnicaService>();
builder.Services.AddScoped<ILiquidacionTecnicaProduccionService, LiquidacionTecnicaProduccionService>();
builder.Services.AddScoped<IIndicadoresProduccionService, IndicadoresProduccionService>();

// Indicador Ecuador Service
builder.Services.AddScoped<IIndicadorEcuadorService, IndicadorEcuadorService>();

// Liquidación Técnica Comparación Service
builder.Services.AddScoped<ILiquidacionTecnicaComparacionService, LiquidacionTecnicaComparacionService>();
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.ILiquidacionTecnicaEcuadorService, ZooSanMarino.Infrastructure.Services.LiquidacionTecnicaEcuadorService>();

// Reporte Técnico Service
builder.Services.AddScoped<IReporteTecnicoService, ReporteTecnicoService>();
builder.Services.AddScoped<ReporteTecnicoExcelService>();

// Reporte Técnico Producción Service
builder.Services.AddScoped<IReporteTecnicoProduccionService, ReporteTecnicoProduccionService>();
builder.Services.AddScoped<ReporteTecnicoProduccionExcelService>();

// Reporte Contable Service
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.IReporteContableService, ZooSanMarino.Infrastructure.Services.ReporteContableService>();
builder.Services.AddScoped<ZooSanMarino.Infrastructure.Services.ReporteContableExcelService>();

// Sistema de Inventario de Aves (ya registrado arriba)

// Guía Genética Service
builder.Services.AddScoped<IGuiaGeneticaService, GuiaGeneticaService>();
builder.Services.AddScoped<IGuiaGeneticaEcuadorService, GuiaGeneticaEcuadorService>();

// Servicios de Traslados
builder.Services.AddScoped<IDisponibilidadLoteService, DisponibilidadLoteService>();
builder.Services.AddScoped<ITrasladoHuevosService, TrasladoHuevosService>();

// Proveedores
builder.Services.AddScoped<IAlimentoNutricionProvider, EfAlimentoNutricionProvider>();
builder.Services.AddScoped<IGramajeProvider, NullGramajeProvider>();


builder.Services.AddScoped<IDbIntrospectionService, DbIntrospectionService>();
builder.Services.AddScoped<IDbSchemaService, DbSchemaService>();
builder.Services.AddScoped<IReadOnlyQueryService, ReadOnlyQueryService>();

// DB Studio Service
builder.Services.AddScoped<IDbStudioService, DbStudioService>();
builder.Services.AddScoped<IMapaService, MapaService>();


// ─────────────────────────────────────
// 9) FluentValidation + HealthChecks
// ─────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<SeguimientoLoteLevanteDtoValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddHealthChecks();

// ─────────────────────────────────────
// 10) Auth (JWT) — ignora preflight OPTIONS
// ─────────────────────────────────────
var keyBytes = Encoding.UTF8.GetBytes(jwt.Key ?? "");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.Zero
        };

        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                Console.WriteLine($"=== JWT OnMessageReceived ===");
                Console.WriteLine($"Request Method: {ctx.Request.Method}");
                Console.WriteLine($"Request Path: {ctx.Request.Path}");
                Console.WriteLine($"Authorization Header: {ctx.Request.Headers.Authorization}");
                Console.WriteLine($"Token: {ctx.Token}");
                
                if (HttpMethods.IsOptions(ctx.Request.Method)) 
                {
                    Console.WriteLine("OPTIONS request - NoResult()");
                    ctx.NoResult();
                }
                else
                {
                    Console.WriteLine("Non-OPTIONS request - continuing");
                }
                
                Console.WriteLine($"=== END JWT OnMessageReceived ===");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"=== JWT OnAuthenticationFailed ===");
                Console.WriteLine($"Exception: {ctx.Exception?.Message}");
                Console.WriteLine($"Request Path: {ctx.Request.Path}");
                Console.WriteLine($"=== END JWT OnAuthenticationFailed ===");
                return Task.CompletedTask;
            }
        };
    });

// ─────────────────────────────────────
// 11) Authorization (allow-all + provider permisivo)
// ─────────────────────────────────────
builder.Services.AddAuthorization(opt =>
{
    var allowAll = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();

    opt.DefaultPolicy  = allowAll;   // [Authorize] sin política
    opt.FallbackPolicy = allowAll;   // endpoints sin atributo
});

// Vital: este provider hace que CUALQUIER [Authorize(Policy="...")] también permita pasar
builder.Services.AddSingleton<IAuthorizationPolicyProvider, AllowAllPolicyProvider>();

// ─────────────────────────────────────
// 12) Swagger + Bearer + CustomSchemaIds + Descarga JSON
// ─────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ZooSanMarino",
        Version = "v1",
        Description = "API de gestión ZooSanMarino (Roles, Usuarios, Granjas, Núcleos, Galpones, Lotes, Inventario, Producción, etc.)"
    });

    // 🔐 Bearer
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme, // "bearer"
        BearerFormat = "JWT",
        Description = "Pega SOLO el token (Swagger añadirá 'Bearer ').",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme }
    };
    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });

    // ✅ Evitar colisiones de schemaId (tipos anidados o repetidos)
    c.CustomSchemaIds(type =>
    {
        var full = type.FullName ?? type.Name;
        full = Regex.Replace(full, @"`\d+", ""); // genéricos
        full = full.Replace("+", ".");           // anidados
        full = full.Replace('.', '_');           // schemaId seguro
        return full;
    });

    // ✅ Configuración para manejar archivos IFormFile
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    // ✅ Configuración para multipart/form-data
    c.OperationFilter<FileUploadOperationFilter>();

    // (Opcional) XML comments
    // var xml = Path.Combine(AppContext.BaseDirectory, "ZooSanMarino.API.xml");
    // if (File.Exists(xml)) c.IncludeXmlComments(xml, includeControllerXmlComments: true);
});

// ─────────────────────────────────────
/* 13) Controllers */
// ─────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Evitar error "positive and negative infinity cannot be written as valid JSON"
        // cuando reportes tienen división por cero (ej. machoIni=0 → ConsAcGrMGUIA infinito)
        options.JsonSerializerOptions.Converters.Add(new ZooSanMarino.API.Infrastructure.JsonDoubleConverter());
        options.JsonSerializerOptions.Converters.Add(new ZooSanMarino.API.Infrastructure.JsonNullableDoubleConverter());
    });

var app = builder.Build();

// ─────────────────────────────────────
/* 14) Pipeline HTTP */
// ─────────────────────────────────────

// 14.0 Manejo de excepciones: 401 para sesión inválida/expirada (evita 500 y obliga a re-login)
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (ex is UnauthorizedAccessException uex)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new { message = uex.Message });
            return;
        }
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { message = ex?.Message ?? "Error interno del servidor." });
    });
});

// 14.0b Routing y CORS (deben ir primero para manejar preflight OPTIONS)
app.UseRouting();
app.UseCors("AppCors");

// ===== Middleware de seguridad (orden importante) =====

// 1. Headers de seguridad HTTP (debe ir temprano)
app.UseSecurityHeaders();

// 2. Rate Limiting (proteger contra DDoS y fuerza bruta)
// DESHABILITADO: Comentado para permitir peticiones sin límites
// app.UseRateLimiting();

// 3. Validar SECRET_UP después de CORS pero antes de Authentication/Authorization
// El middleware ya maneja OPTIONS requests internamente
app.UsePlatformSecret();

// 14.1 CSS para tema oscuro de Swagger UI (sin archivos estáticos)
const string swaggerDarkCss = """
:root {
  --swagger-font-size: 14px;
}
body.swagger-ui, .swagger-ui .topbar { background: #0f172a !important; color: #e5e7eb !important; }
.swagger-ui .topbar { border-bottom: 1px solid #1f2937; }
.swagger-ui .topbar .download-url-wrapper .select-label select { background: #111827; color:#e5e7eb; }
.swagger-ui .info, .swagger-ui .opblock, .swagger-ui .model, .swagger-ui .opblock-tag { color: #e5e7eb; }
.swagger-ui .opblock { background:#111827; border-color:#374151; }
.swagger-ui .opblock .opblock-summary { background:#0b1220; }
.swagger-ui .opblock .opblock-summary-method { background:#1f2937; }
.swagger-ui .responses-inner, .swagger-ui .parameters-container { background:#0b1220; }
.swagger-ui .tab li { color:#e5e7eb; }
.swagger-ui .btn, .swagger-ui select, .swagger-ui input { background:#1f2937; color:#e5e7eb; border-color:#374151; }
.swagger-ui .response-control-media-type__accept-message { color:#9ca3af; }
.swagger-ui .opblock-tag { background:#0b1220; border:1px solid #1f2937; border-radius:6px; padding:8px 12px; }
""";
app.MapGet("/swagger-ui/dark.css", () => Results.Text(swaggerDarkCss, "text/css"));

// 14.2 Endpoint de login para Swagger (DEBE ir ANTES del middleware)
app.MapPost("/swagger/login", async (HttpContext context, IConfiguration config) =>
{
    var form = await context.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var expectedPassword = config["Swagger:Password"] ?? "Swagger2024!SanMarino#API";
    var cookieName = config["Swagger:SessionCookieName"] ?? "SwaggerAuth";

    if (password == expectedPassword)
    {
        // Detectar HTTPS vía proxy
        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var isHttpsViaProxy = string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
        var isSecure = context.Request.IsHttps || isHttpsViaProxy;
        
        // Crear cookie de autenticación
        var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(expectedPassword + context.Connection.RemoteIpAddress?.ToString()));
        var hashString = Convert.ToBase64String(hash);
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, // Previene acceso desde JavaScript (protección XSS)
            Secure = isSecure, // true en producción con HTTPS
            SameSite = SameSiteMode.Strict, // Más estricto para cookies de autenticación
            Expires = DateTimeOffset.UtcNow.AddMinutes(6), // 6 minutos de sesión con renovación automática
            Path = "/" // Aplicar a todo el sitio
        };

        context.Response.Cookies.Append(cookieName, hashString, cookieOptions);

        // Crear cookie de última actividad para tracking de inactividad
        var lastActivityKey = $"{cookieName}_LastActivity";
        var lastActivityOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure, // Debe ser Secure también
            SameSite = SameSiteMode.Strict, // Más estricto para cookies de sesión
            Expires = DateTimeOffset.UtcNow.AddMinutes(6),
            Path = "/"
        };
        context.Response.Cookies.Append(lastActivityKey, DateTime.UtcNow.ToString("O"), lastActivityOptions);
        context.Response.Redirect("/swagger");
        return;
    }

    // Contraseña incorrecta - redirigir a login con error
    context.Response.Redirect("/swagger?error=Contraseña incorrecta");
});

// 14.2.1 Protección de Swagger con contraseña (DEBE ir ANTES de UseSwagger)
app.UseMiddleware<SwaggerPasswordMiddleware>();

// 14.3 Swagger JSON como descarga forzada (protegido por middleware)
app.MapGet("/swagger/download", (ISwaggerProvider provider) =>
{
    var doc = provider.GetSwagger("v1");
    using var sw = new StringWriter();
    var w = new Microsoft.OpenApi.Writers.OpenApiJsonWriter(sw);
    doc.SerializeAsV3(w);
    var bytes = Encoding.UTF8.GetBytes(sw.ToString());
    return Results.File(bytes, "application/json", "swagger-v1.json");
});

// 14.4 Swagger y UI (protegido por middleware)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    // Documento principal
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZooSanMarino v1");

    // UI
    c.DocumentTitle = "ZooSanMarino — API Docs";
    c.DisplayRequestDuration();
    c.EnableFilter();                 // caja de búsqueda/filtrado
    c.EnableDeepLinking();            // anclas navegables
    c.DefaultModelExpandDepth(1);     // menos ruido en modelos
    c.DefaultModelsExpandDepth(-1);   // oculta la sección "Schemas" por defecto
    c.DocExpansion(DocExpansion.List);

    // Tema oscuro
    c.InjectStylesheet("/swagger-ui/dark.css");

    // (Opcional) Ruta: deja /swagger como UI
    // c.RoutePrefix = string.Empty; // si quieres la UI en "/"
});

// Routing, CORS y SECRET_UP ya fueron configurados arriba (líneas 350-357)

app.UseAuthentication();

// Resuelve CompanyId efectivo desde X-Active-Company para toda la app
app.UseMiddleware<ZooSanMarino.API.Infrastructure.ActiveCompanyMiddleware>();

app.UseAuthorization();

// Health
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/hc");

// Debug JWT
app.MapGet("/debug/jwt", (IOptions<JwtOptions> opt) =>
{
    var o = opt.Value;
    string Mask(string s) => string.IsNullOrEmpty(s) ? "" : $"{s[..Math.Min(4, s.Length)]}***{s[^Math.Min(4, s.Length)..]}";
    return Results.Ok(new
    {
        Issuer = o.Issuer,
        Audience = o.Audience,
        Duration = o.DurationInMinutes,
        KeyMasked = Mask(o.Key ?? ""),
        KeyLength = o.Key?.Length ?? 0
    });
});

// Debug ConnectionString
app.MapGet("/debug/config/conn", (IConfiguration cfg) =>
{
    var raw = cfg.GetConnectionString("ZooSanMarinoContext")
           ?? cfg["ConnectionStrings:ZooSanMarinoContext"]
           ?? cfg["ZOO_CONN"];

    var safe = string.IsNullOrEmpty(raw)
        ? ""
        : Regex.Replace(raw, "(Password=)([^;]+)", "$1******", RegexOptions.IgnoreCase);

    return Results.Ok(new { ConnectionString = safe });
});

// Ping DB
app.MapGet("/db-ping", async (ZooSanMarinoContext ctx) =>
{
    try
    {
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.CloseConnectionAsync();
        return Results.Ok(new { status = "ok", db = "reachable" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"DB unreachable: {ex.Message}");
    }
});

// Endpoints de seguridad estándar
// security.txt - RFC 9116
app.MapGet("/.well-known/security.txt", () =>
{
    var securityTxt = @"Contact: mailto:security@example.com
Expires: 2026-12-31T23:59:59.000Z
Preferred-Languages: es, en
Canonical: https://example.com/.well-known/security.txt
Policy: https://example.com/security-policy

# Nota: Actualizar con información de contacto real de seguridad";
    return Results.Text(securityTxt, "text/plain");
});

// robots.txt
app.MapGet("/robots.txt", () =>
{
    var robotsTxt = @"# robots.txt para ZooSanMarino API
User-agent: *
Allow: /api/
Disallow: /swagger/
Disallow: /api/auth/
Disallow: /api/Admin/
Disallow: /.well-known/

# Permitir acceso a endpoints públicos si existen
Allow: /api/health
Allow: /api/db-ping

# Bloquear crawlers de endpoints sensibles
User-agent: *
Disallow: /api/*/password
Disallow: /api/*/token
Disallow: /api/*/secret";
    return Results.Text(robotsTxt, "text/plain");
});

// ─────────────────────────────────────
// 15) Controllers (DEBE ir ANTES del catch-all OPTIONS)
// ─────────────────────────────────────
// El catch-all {*path} con OPTIONS devuelve 405 para otros métodos si se registra antes;
// al registrar MapControllers primero, las rutas api/* tienen prioridad.
app.MapControllers();

// Catch-all OPTIONS (necesario para CORS preflight) - DEBE ir después de MapControllers
// OPTIONS está habilitado intencionalmente para soportar CORS preflight requests
app.MapMethods("{*path}", new[] { "OPTIONS" }, () => Results.Ok()).RequireCors("AppCors");

// ─────────────────────────────────────
// 16) Migrar + Seed (flags)
// ─────────────────────────────────────
bool runMigrations = app.Configuration.GetValue<bool>("Database:RunMigrations");
bool runSeed       = app.Configuration.GetValue<bool>("Database:RunSeed");

if (runMigrations || runSeed)
{
    await app.MigrateAndSeedAsync();
}
app.Run();


// ─────────────────────────────────────
// Extensión: CORS desde lista de orígenes
// ─────────────────────────────────────
internal static class CorsExtensions
{
    public static void AddCorsFromOrigins(this IServiceCollection services, string policyName, string[] origins)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(policyName, policy =>
            {
                if (origins is null || origins.Length == 0 || Array.Exists(origins, x => x == "*"))
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
                else
                {
                    policy.WithOrigins(origins)
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                    // Si usas cookies: .AllowCredentials()
                }
            });
        });
    }
}

// ─────────────────────────────────────
// Policy Provider permisivo para DEV
//    - Hace que cualquier [Authorize(Policy="...")] permita pasar.
// ─────────────────────────────────────
internal sealed class AllowAllPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly AuthorizationPolicy _allowAll =
        new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public AllowAllPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => Task.FromResult(_allowAll);

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => Task.FromResult<AuthorizationPolicy?>(_allowAll);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        => Task.FromResult<AuthorizationPolicy?>(_allowAll);
}
