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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.Swagger;          // ISwaggerProvider (para /swagger/download)
using Swashbuckle.AspNetCore.SwaggerUI;       // Opciones UI

using ZooSanMarino.API.Extensions;
using ZooSanMarino.API.Infrastructure;
using ZooSanMarino.API.Configuration;
using ZooSanMarino.API.Middleware;
using ZooSanMarino.Application.Calculos;
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
// Pool de contextos: el DbContext solo tiene el ctor de opciones y ningún estado por request
// (sin campos inyectados, sin SetCommandTimeout), así que reutilizar la instancia es seguro y
// evita reconstruirla en cada request.
//
// ⛔ NO agregar EnableRetryOnFailure acá. La estrategia de reintento no soporta transacciones
// abiertas por el usuario, y el repo tiene 67 BeginTransaction en ~25 services (inventario,
// engorde, traslados, cuadre) contra 1 solo CreateExecutionStrategy: activarla haría lanzar
// InvalidOperationException en runtime en todos esos caminos de escritura. Si algún día se
// quiere el reintento, primero hay que envolver cada transacción en una execution strategy.
builder.Services.AddDbContextPool<ZooSanMarinoContext>(opts =>
{
    opts.UseSnakeCaseNamingConvention()
        .UseNpgsql(conn);
    ZooSanMarinoContext.ConfigurarWarnings(opts);
});

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

// ── Transporte de correo saliente ─────────────────────────────────────────────
// SMTP con STARTTLS contra Office 365 (587). Verificado el 05-ago-2026: este código envía
// correctamente con las credenciales de producción. Un rechazo 5.7.139 / 5.7.57 es una política
// del tenant según el origen de la conexión, no un problema del código ni de la contraseña.
// Ver: backend/documentacion/DIAGNOSTICO_CORREO_OFFICE365.md
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// La configuración incompleta NO tumba el arranque (el HostedService moriría con la app en ECS):
// se avisa y los correos quedan "pending" con el motivo en email_queue.error_message.
if (!EnvioCorreoCalculos.HayConfiguracionSmtp(
        builder.Configuration["Email:Smtp:Host"],
        builder.Configuration["Email:Smtp:Username"],
        builder.Configuration["Email:Smtp:Password"]))
{
    Console.WriteLine($"🔴 {EnvioCorreoCalculos.DiagnosticoSinConfiguracion()}");
}

// Registrar procesador de cola de correos solo si está habilitado por configuración
var emailQueueEnabled = builder.Configuration.GetValue<bool?>("Email:Queue:Enabled") ?? false;
if (emailQueueEnabled)
{
    builder.Services.AddHostedService<ZooSanMarino.API.BackgroundServices.EmailQueueProcessorService>(); // Procesador de cola en segundo plano
}
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserFarmService, UserFarmService>();
builder.Services.AddScoped<IUserFarmScopeService, UserFarmScopeService>();
builder.Services.AddScoped<ILocationScopeResolver, LocationScopeResolver>(); // alcance granular núcleo/galpón/lote (caché por request)
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ICompanyMenuService, CompanyMenuService>();
builder.Services.AddScoped<ICompanyPermissionService, CompanyPermissionService>();
builder.Services.AddScoped<IFarmService, FarmService>();
builder.Services.AddScoped<IDashboardService, DashboardService>(); // dashboard: alcance por empresa + ubicación
builder.Services.AddScoped<INucleoService, NucleoService>();
builder.Services.AddScoped<IGalponService, GalponService>();
builder.Services.AddScoped<ILoteService, LoteService>();
builder.Services.AddScoped<ILotePosturaLevanteService, LotePosturaLevanteService>();
builder.Services.AddScoped<ILotePosturaProduccionService, LotePosturaProduccionService>();
builder.Services.AddScoped<ILoteFormDataService, LoteFormDataService>();
builder.Services.AddScoped<ILoteAveEngordeService, LoteAveEngordeService>();
builder.Services.AddScoped<ILoteBaseEngordeService, LoteBaseEngordeService>();
builder.Services.AddScoped<ILoteReproductoraService, LoteReproductoraService>();
builder.Services.AddScoped<ILoteReproductoraFilterDataService, LoteReproductoraFilterDataService>();
builder.Services.AddScoped<ILoteReproductoraAveEngordeService, LoteReproductoraAveEngordeService>();
builder.Services.AddScoped<ICorreccionAvesDisponiblesEngordeService, CorreccionAvesDisponiblesEngordeService>();
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
builder.Services.AddScoped<ILotePosturaBaseService, LotePosturaBaseService>();
builder.Services.AddScoped<ISeguimientoDiarioService, SeguimientoDiarioService>();
builder.Services.AddScoped<IMasterListService, MasterListService>();
// Sistema de Inventario de Aves (registrado antes para inyección en seguimientos)
builder.Services.AddScoped<IInventarioAvesService, InventarioAvesService>();
builder.Services.AddScoped<IHistorialInventarioService, HistorialInventarioService>();
builder.Services.AddScoped<IMovimientoAvesService, MovimientoAvesService>();
builder.Services.AddScoped<IMovimientoPolloEngordeService, MovimientoPolloEngordeService>();
builder.Services.AddScoped<IMovimientoPolloEngordePanamaService, MovimientoPolloEngordePanamaService>();
builder.Services.AddScoped<IMovimientoPolloEngordeFilterDataService, MovimientoPolloEngordeFilterDataService>();
builder.Services.AddScoped<IInventarioGastoService, InventarioGastoService>();
builder.Services.AddScoped<IVacunacionCronogramaService, VacunacionCronogramaService>();
builder.Services.AddScoped<IVacunacionRegistroService, VacunacionRegistroService>();
builder.Services.AddScoped<IVacunacionReportesService, VacunacionReportesService>();
builder.Services.AddScoped<IVacunacionPlantillaService, VacunacionPlantillaService>();
builder.Services.AddScoped<IVacunacionMaterializadorService, VacunacionMaterializadorService>();
builder.Services.AddScoped<IImplementacionService, ImplementacionService>();

builder.Services.AddScoped<ISeguimientoLoteLevanteService, SeguimientoLoteLevanteService>();
// Push de capturas offline (PWA F3).
builder.Services.AddScoped<ISyncPushService, SyncPushService>();
builder.Services.AddScoped<ISeguimientoAvesEngordeService, SeguimientoAvesEngordeService>();
builder.Services.AddScoped<ISeguimientoAvesEngordeFilterDataService, SeguimientoAvesEngordeFilterDataService>();
builder.Services.AddScoped<ISeguimientoDiarioEngordeService, SeguimientoDiarioEngordeService>();
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
// Fase 2 (S3): descuento/devolución automáticos del inventario Colombia (modelo A) desde
// seguimientos. NO abre tx propia; participa de la tx externa del servicio de seguimiento.
// (Fase 3 paso 2: Colombia migró a modelo B; este servicio queda registrado pero ya no lo llama Colombia.)
builder.Services.AddScoped<IFarmInventoryConsumoService, FarmInventoryConsumoService>();
// Fase 3 (paso 2): descuento/devolución del inventario Colombia en el MODELO B unificado
// (nivel granja, id-mapping A→B). Reemplaza a FarmInventoryConsumoService para lotes Colombia.
builder.Services.AddScoped<IColombiaInventarioConsumoService, ColombiaInventarioConsumoService>();
builder.Services.AddScoped<IFarmInventoryReportService, FarmInventoryReportService>();
builder.Services.AddScoped<IInventarioGestionService, InventarioGestionService>();
// Silos y bodegas: lista maestra + asignación a granja, galpón y lote (empresas con
// ManejaInventarioPorSilo). GalponSilo y LoteSilo dependen de IFarmSiloService, así que va primero.
builder.Services.AddScoped<ISiloCatalogoService, SiloCatalogoService>();
builder.Services.AddScoped<IFarmSiloService, FarmSiloService>();
builder.Services.AddScoped<IGalponSiloService, GalponSiloService>();
builder.Services.AddScoped<ILoteSiloService, LoteSiloService>();
// F7.3 — qué tipos de huevo produce cada lote (lista blanca del diario de producción).
builder.Services.AddScoped<ILoteHuevoItemService, LoteHuevoItemService>();
builder.Services.AddScoped<ICuadreAlimentoEngordeService, CuadreAlimentoEngordeService>();
// Doble validación de los seguimientos diarios: separa al guardar, descuenta al validar.
builder.Services.AddScoped<IValidacionSeguimientoService, ValidacionSeguimientoService>();
builder.Services.AddScoped<IItemInventarioService, ItemInventarioService>();
builder.Services.AddScoped<IPermissionService, PermissionService>(); 

// ✅ Servicio orquestador único de roles/permissions/menús
builder.Services.AddScoped<IRoleCompositeService, RoleCompositeService>();

// Producción Avícola Raw
builder.Services.AddScoped<IProduccionAvicolaRawService, ProduccionAvicolaRawService>();

// Excel Import Service
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();

// Migraciones Masivas (módulo independiente)
builder.Services.AddScoped<IMigracionRepository, MigracionRepository>();
builder.Services.AddScoped<IMigracionService, MigracionService>();

// Puente de consulta ZooPanamaPollo → pollo engorde (SOLO LECTURA del origen; sincronización idempotente)
builder.Services.AddHttpClient<IPuentePanamaApiClient, PuentePanamaApiClient>();
builder.Services.AddScoped<IPuentePanamaService, PuentePanamaService>();

// Liquidación Técnica Service
builder.Services.AddScoped<ILiquidacionTecnicaService, LiquidacionTecnicaService>();
builder.Services.AddScoped<IIndicadoresProduccionService, IndicadoresProduccionService>();

// Indicador Ecuador Service
builder.Services.AddScoped<IIndicadorEngordeService, IndicadorEngordeService>();

// Informe Semanal Pollo de Engorde (Panamá)
builder.Services.AddScoped<IInformeSemanalPolloEngordeService, InformeSemanalPolloEngordeService>();

// Reporte Diario Costos Pollo Engorde (por granja + lote base)
builder.Services.AddScoped<IReporteDiarioCostosEngordeService, ReporteDiarioCostosEngordeService>();

// Reporte Diario Área de Costos POSTURA (levante + producción, por lote base)
builder.Services.AddScoped<IReporteDiarioCostosPosturaService, ReporteDiarioCostosPosturaService>();

// Reporte Técnico Semanal (Sanmarino postura: Levante + Producción vs guía genética)
builder.Services.AddScoped<IReporteTecnicoSemanalService, ReporteTecnicoSemanalService>();

// Reporte Indicador Panamá Service (liquidación Pollo Engorde Panamá)
builder.Services.AddScoped<IReporteIndicadorPanamaService, ReporteIndicadorPanamaService>();

// Liquidación Técnica Comparación Service
builder.Services.AddScoped<ILiquidacionTecnicaComparacionService, LiquidacionTecnicaComparacionService>();
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.ILiquidacionTecnicaEngordeService, ZooSanMarino.Infrastructure.Services.LiquidacionTecnicaEngordeService>();

// Liquidación Cierre Lote Levante
builder.Services.AddScoped<ILiquidacionCierreLoteLevanteService, LiquidacionCierreLoteLevanteService>();

// Reporte Técnico Service
builder.Services.AddScoped<IReporteTecnicoService, ReporteTecnicoService>();
builder.Services.AddScoped<ReporteTecnicoExcelService>();

// Reporte Técnico Producción Service
builder.Services.AddScoped<IReporteTecnicoProduccionService, ReporteTecnicoProduccionService>();
builder.Services.AddScoped<ReporteTecnicoProduccionExcelService>();

// Exportación Excel — Nuevo servicio (LEVANTE tabs + PRODUCCIÓN tabs)
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.IExportacionExcelService,
    ZooSanMarino.Infrastructure.Services.ExportacionExcelService>();

// Reporte Contable Service
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.IReporteContableService, ZooSanMarino.Infrastructure.Services.ReporteContableService>();
builder.Services.AddScoped<ZooSanMarino.Infrastructure.Services.ReporteContableExcelService>();

// Sistema de Inventario de Aves (ya registrado arriba)

// Guía Genética Service
builder.Services.AddScoped<IGuiaGeneticaService, GuiaGeneticaService>();
builder.Services.AddScoped<IGuiaGeneticaEngordeService, GuiaGeneticaEngordeService>();

// Guía Genética REDUCIDA (guia_genetica_santa_reyes): la puerta de escritura que la tabla no tenía.
builder.Services.AddScoped<IGuiaGeneticaSantaReyesService, GuiaGeneticaSantaReyesService>();

// Perfil de guía genética de la empresa activa: lo consumen los guards fail-closed de los
// controllers de guía (reducida ⇄ compartida). Ver GuiaGeneticaEscrituraGuard.
builder.Services.AddScoped<IGuiaGeneticaPerfilResolver, GuiaGeneticaPerfilResolver>();

// Servicios de Traslados
builder.Services.AddScoped<IDisponibilidadLoteService, DisponibilidadLoteService>();
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.IEspejoHuevoProduccionSyncService, ZooSanMarino.Infrastructure.Services.EspejoHuevoProduccionSyncService>();
// Arrastre de los huevos capturados en levante (semana 14+) al primer registro de producción al liquidar.
builder.Services.AddScoped<ZooSanMarino.Application.Interfaces.IArrastreHuevosLevanteService, ZooSanMarino.Infrastructure.Services.ArrastreHuevosLevanteService>();
builder.Services.AddScoped<ITrasladoHuevosService, TrasladoHuevosService>();
builder.Services.AddScoped<ITrasladoAvesDesdeSegService, TrasladoAvesDesdeSegService>();

// Proveedores
builder.Services.AddScoped<IAlimentoNutricionProvider, EfAlimentoNutricionProvider>();
builder.Services.AddScoped<IGramajeProvider, NullGramajeProvider>();


// ===================== DB Studio =====================
builder.Services.Configure<ZooSanMarino.Infrastructure.DbStudio.DbStudioOptions>(
    builder.Configuration.GetSection("DbStudio"));
builder.Services.AddSingleton<ZooSanMarino.Infrastructure.DbStudio.DbStudioRuntime>();
builder.Services.AddScoped<IDbStudioAuthorization, DbStudioAuthorization>();
builder.Services.AddScoped<IDbStudioService, DbStudioService>();
builder.Services.AddScoped<IDbStudioPermissionService, DbStudioPermissionService>();
builder.Services.AddScoped<IDbStudioConcurrencyService, DbStudioConcurrencyService>();

builder.Services.AddScoped<IMapaService, MapaService>();

// Gestión de Clientes
builder.Services.AddScoped<IClienteService, ClienteService>();

// Lesiones (Panamá — tab Seguimiento Diario Reproductora/Apoyo/Engorde)
builder.Services.AddScoped<ILesionService, LesionService>();

// Módulo de tickets de soporte / requerimientos
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ITicketPerfilService, TicketPerfilService>();
builder.Services.AddScoped<ITicketTareaService, TicketTareaService>();
// ItalJira — historias (épicas) del área de desarrollo
builder.Services.AddScoped<IHistoriaService, HistoriaService>();

// PAT / Tokens de servicio (clientes headless: crones que llaman /api/tickets)
builder.Services.AddScoped<IServiceTokenService, ServiceTokenService>();

// B1 — sesiones revocables (lista blanca por `jti`). La consulta la cachea el propio service.
builder.Services.AddScoped<ISesionActivaService, SesionActivaService>();


// ─────────────────────────────────────
// 9) FluentValidation + HealthChecks
// ─────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<SeguimientoLoteLevanteDtoValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddHealthChecks();

// ─────────────────────────────────────
// 10) Auth (JWT + Service Token) — ignora preflight OPTIONS
// ─────────────────────────────────────
// Policy scheme "Smart": reenvía por prefijo del header Authorization.
//   - "Bearer sk_..."  → esquema "ServiceToken" (PAT de larga duración, solo /api/tickets).
//   - cualquier otro   → JwtBearer (config existente TAL CUAL).
// La config del JWT NO cambia; solo se movió dentro de esta cadena.
var keyBytes = Encoding.UTF8.GetBytes(jwt.Key ?? "");
builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = "Smart";
        o.DefaultChallengeScheme = "Smart";
    })
    .AddPolicyScheme("Smart", "JWT or ServiceToken", o =>
    {
        o.ForwardDefaultSelector = ctx =>
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            return auth.StartsWith("Bearer sk_", StringComparison.OrdinalIgnoreCase)
                ? ZooSanMarino.Infrastructure.Auth.ServiceTokenAuthHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme;
        };
    })
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
            // No se loguean tokens ni el header Authorization (evita filtrar credenciales).
            OnMessageReceived = ctx =>
            {
                // El preflight CORS (OPTIONS) no lleva token: no intentar autenticarlo.
                if (HttpMethods.IsOptions(ctx.Request.Method))
                    ctx.NoResult();
                return Task.CompletedTask;
            },

            // B1 — REVOCACIÓN. Firma y `exp` válidos ya no alcanzan: la sesión tiene que seguir
            // viva en `sesiones_activas`. Va acá y no en un middleware por dos razones: corre DENTRO
            // de UseAuthentication() —o sea antes de resolver empresa activa y de evaluar permisos— y
            // cubre TODOS los endpoints, incluidos los que todavía no existen (un middleware con
            // lista de rutas se desactualiza).
            // El esquema ServiceToken (PAT `sk_…`) NO pasa por acá: tiene su propia revocación.
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirst(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

                var expiracion = (ctx.SecurityToken as Microsoft.IdentityModel.JsonWebTokens.JsonWebToken)?.ValidTo
                    ?? DateTime.UtcNow;

                var sesiones = ctx.HttpContext.RequestServices.GetRequiredService<ISesionActivaService>();
                var estado = await sesiones.EvaluarAsync(jti, expiracion, ctx.HttpContext.RequestAborted);

                if (RevocacionSesionCalculos.EsSesionValida(estado))
                    return;

                // El motivo viaja por Items hasta OnChallenge, que es quien escribe la respuesta.
                ctx.HttpContext.Items[RevocacionSesionCalculos.MotivoRevocada] =
                    RevocacionSesionCalculos.MotivoParaCliente(estado);
                ctx.Fail("Sesión revocada o no registrada.");
            },

            // Mismo contrato que PlatformSecretMiddleware: cabecera X-Auth-Failure + `errorCode` en
            // el CUERPO (en dev el front es otro origen y no puede leer cabeceras personalizadas).
            // Sólo se toca la respuesta cuando el rechazo es NUESTRO; el resto de los 401 del
            // JwtBearer siguen saliendo exactamente igual que antes.
            OnChallenge = async ctx =>
            {
                if (ctx.HttpContext.Items[RevocacionSesionCalculos.MotivoRevocada] is not string motivo)
                    return;

                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                ctx.Response.Headers[PlatformSecretMiddleware.AuthFailureHeader] = motivo;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    error = "Unauthorized",
                    errorCode = motivo,
                    message = motivo == RevocacionSesionCalculos.MotivoRevocada
                        ? "La sesión fue cerrada. Inicia sesión de nuevo."
                        : "La sesión expiró. Inicia sesión de nuevo."
                });
            }
        };
    })
    // Esquema de PAT (Service Token): activado por el policy scheme "Smart" cuando el header
    // empieza con "Bearer sk_". El handler valida el token y limita el alcance a /api/tickets.
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        ZooSanMarino.Infrastructure.Auth.ServiceTokenAuthHandler>(
            ZooSanMarino.Infrastructure.Auth.ServiceTokenAuthHandler.SchemeName, null);

// ─────────────────────────────────────
// 11) Authorization (deny-by-default)
// ─────────────────────────────────────
// Antes existía un "allow-all" (DefaultPolicy/FallbackPolicy => true + AllowAllPolicyProvider)
// que hacía que [Authorize] y [Authorize(Policy=...)] dejaran pasar a CUALQUIERA (incluso anónimos).
// Ahora:
//   - DefaultPolicy = RequireAuthenticatedUser (default del framework): [Authorize] exige token válido.
//   - FallbackPolicy = RequireAuthenticatedUser: endpoints sin atributo también exigen sesión,
//     salvo los marcados explícitamente con [AllowAnonymous] / .AllowAnonymous().
builder.Services.AddAuthorization(opt =>
{
    opt.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Políticas con nombre usadas por los controllers (Menu/Role/...). Antes las "resolvía"
    // el AllowAllPolicyProvider dejándolas pasar; ahora exigen, como mínimo, usuario autenticado.
    //
    // 🔴 Estas tres NO son el gate de nada por sí solas: son "token válido y nada más". Se conservan
    // porque los atributos [Authorize(Policy = ...)] las nombran, pero el permiso real se exige así:
    //
    //   - CanManageRoles / CanManageMenus (RolesController, MenuController.GetTree)
    //       => RolesGestionFilterAttribute (API/Infrastructure/RolesGestionFilter.cs), con la regla
    //          pura en RolesAutorizacionCalculos. Keys `roles.gestionar` y `menus.gestionar`.
    //          Medido el 5-sep-2026: sin ese filtro, cualquier sesión autenticada podía hacer
    //          POST /api/Roles/{id}/permissions/assign — o sea asignarse permisos, volver a loguearse
    //          (las keys se hornean como claims al login) y saltarse todos los demás gates.
    //
    //   - CanManageUsers => sigue siendo sólo "autenticado", a propósito y fuera de alcance: la
    //       comparten RolesController.MenusForUser y MenuController.GetForUser, ajenos al módulo de
    //       usuarios; el módulo en sí lo cierra GestionUsuariosEscrituraFilterAttribute.
    //
    // Por qué el gate no puede vivir acá: una policy no distingue lectura de escritura, y la LECTURA
    // de roles necesita una OR con `usuarios.gestionar` que la escritura no debe tener (el modal de
    // usuarios consume GET /api/Roles para su desplegable). Ver RolesAutorizacionCalculos.
    foreach (var policyName in new[] { "CanManageMenus", "CanManageUsers", "CanManageRoles" })
        opt.AddPolicy(policyName, p => p.RequireAuthenticatedUser());

    // ESCRITURA sobre los catálogos GLOBALES (permisos y menús): solo el administrador de la
    // aplicación. Son estructuras compartidas por todas las empresas — borrar una key de permiso o
    // un ítem de menú se lo lleva puesto a todos los países a la vez. Antes esto no estaba cerrado
    // por ningún lado: PermissionController no tenía un solo [Authorize] y CanManageMenus era
    // "usuario autenticado", así que cualquier sesión válida podía escribir el catálogo.
    // Las LECTURAS quedan como estaban a propósito: un usuario no admin necesita GET /api/Permission
    // para asignarle permisos a un rol y GET /api/Menu/tree para ver etiquetas en la tabla de roles.
    // La regla vive en Application/Calculos (pura y con tests), no acá.
    opt.AddPolicy("AdminAplicacion", p => p.RequireAssertion(ctx =>
        CatalogoGlobalAutorizacionCalculos.PuedeEscribirCatalogoGlobal(
            ctx.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value))));

    // ESCRITURA sobre las EMPRESAS: crearlas, editarlas, borrarlas y decidir qué menús y permisos
    // tiene cada una. Hasta el 4-sep-2026 CompanyController no tenía un solo [Authorize], así que
    // cualquier sesión válida podía hacer PUT /api/Company/{id}/menus sobre CUALQUIER empresa — o
    // sea, reasignarse módulos a sí misma o tocar los de otro país.
    // Dos ejes: el DATO (users.is_super_admin, que viaja como claim) o el rol de administrador de
    // la aplicación. Las LECTURAS quedan abiertas a propósito: GET /api/Company alimenta el selector
    // de empresa activa y GET /api/Company/global alimenta el filtro del módulo de Tickets.
    // La regla vive en Application/Calculos (pura y con tests), no acá.
    opt.AddPolicy("AdminEmpresas", p => p.RequireAssertion(ctx =>
        AdministracionEmpresasAutorizacionCalculos.PuedeAdministrarEmpresas(
            AdministracionEmpresasAutorizacionCalculos.LeerMarcaSuperAdmin(
                ctx.User.FindFirst("is_super_admin")?.Value),
            ctx.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value))));
});

// ─────────────────────────────────────
// 12) Swagger + Bearer + CustomSchemaIds + Descarga JSON
// ─────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ItalGranja API",
        Version = "v1",
        Description = """
            API ItalGranja — gestión avícola (roles, usuarios, granjas, lotes, inventario, producción, etc.)

            ### Cómo probar desde acá

            1. **Token** — `POST /swagger/token` con `{ "email": "...", "password": "..." }` (texto plano,
               sólo en ambientes de desarrollo). Devuelve el JWT. `POST /api/Auth/login` **no sirve desde
               Swagger**: recibe el cuerpo cifrado por el front.
            2. **Authorize** — pegá el token en el candado de arriba (sólo el token, sin `Bearer `).
            3. **Try it out** — ya funciona: esta UI agrega sola la firma de plataforma (`X-Secret-Up`)
               que el backend exige en todo `/api/*`.

            ### Alcance por empresa

            Casi todo endpoint responde **según la empresa activa**. Cada operación acepta tres cabeceras
            opcionales para elegirla: `X-Active-Company` (nombre), `X-Active-Company-Id` (id) y
            `X-Active-Pais`. Sin ellas se usa la empresa del token.

            Son una petición, no una orden: el backend sólo la acepta si el usuario pertenece a esa
            empresa (o es super admin). Pedir una empresa ajena **no** amplía el alcance — se cae al
            del token.
            """
    });

    // 🔐 Bearer
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme, // "bearer"
        BearerFormat = "JWT",
        Description = "Pega SOLO el token (Swagger añadirá 'Bearer ')."
    };
    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
    // Microsoft.OpenApi v2: la referencia al esquema se hace por ID vía OpenApiSecuritySchemeReference
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, doc), new List<string>() }
    });

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
        Type = JsonSchemaType.String,
        Format = "binary"
    });

    // ✅ Configuración para multipart/form-data
    c.OperationFilter<FileUploadOperationFilter>();

    // 🏢 Cabeceras de empresa/país activas: son el filtro multiempresa de casi todo el API y hasta
    // el 4-sep-2026 no estaban declaradas en el contrato, así que desde Swagger no se podía cambiar
    // de empresa — o sea, no se podía probar el escenario que más importa en este sistema.
    c.OperationFilter<EmpresaActivaHeadersOperationFilter>();

    // 📖 Los doc-comments de los controllers (79 de 94 los tienen, varios explican justamente el
    // alcance por empresa) sólo llegan a la UI si el .csproj emite el XML y se incluye acá. Estuvo
    // comentado desde siempre: la documentación existía escrita y no la veía nadie.
    var xml = Path.Combine(AppContext.BaseDirectory, "ZooSanMarino.API.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml, includeControllerXmlComments: true);
});

// ─────────────────────────────────────
/* 12b) Compresión de respuesta
   El API viaja sin comprimir: no hay proxy delante (el Dockerfile publica el binario directo) y
   el ALB no comprime por su cuenta. Los payloads gordos son los reportes en JSON, que comprimen
   muy bien. Brotli primero, gzip como fallback para clientes viejos.
   EnableForHttps: el ALB termina TLS y habla HTTP con la tarea, pero cuando se llega por HTTPS
   directo también queremos comprimir. Los cuerpos con secretos (login) son chicos y no reflejan
   entrada del atacante, que es lo que haría explotable un BREACH. */
// ─────────────────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    // Los MimeTypes por defecto no incluyen todo lo que devolvemos.
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults
        .MimeTypes.Concat(new[]
        {
            "application/json",
            "application/problem+json",
            "text/csv"
        });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(
    o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(
    o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

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
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // [ApiController] responde 400 solo (ValidationProblemDetails: {title, errors}) cuando el
        // JSON del body no calza con el contrato ANTES de que el action corra — ej. un decimal
        // tipeado en un campo `int` (caso real: liquidación Panamá, 26-ago-2026, "avesFinalGranja").
        // Esa forma no tiene `error` ni `message`, así que el front cae al genérico de Angular
        // ("Http failure response for URL: 400 OK") sin decir nada. Se reescribe con la MISMA forma
        // {error} que ya usan todos los controllers, nombrando el campo que falló.
        options.InvalidModelStateResponseFactory = context =>
        {
            var detalles = context.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .Select(kv =>
                {
                    var campo = kv.Key.TrimStart('$', '.');
                    var err = kv.Value!.Errors[0];
                    var texto = !string.IsNullOrWhiteSpace(err.ErrorMessage)
                        ? err.ErrorMessage
                        : err.Exception?.Message ?? "dato inválido";
                    return string.IsNullOrWhiteSpace(campo) ? texto : $"{campo}: {texto}";
                })
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
            var mensaje = detalles.Count > 0
                ? "Solicitud inválida — " + string.Join(" | ", detalles)
                : "Solicitud inválida: revise los datos enviados.";
            return new BadRequestObjectResult(new { error = mensaje });
        };
    });

var app = builder.Build();

// ─────────────────────────────────────
// 13.1) Dev bootstrap (solo Development)
// Crea tabla faltante cuando la BD no tiene historia EF alineada
// ─────────────────────────────────────
if (app.Environment.EnvironmentName == "Development")
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZooSanMarinoContext>();
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS public.lote_postura_base (
  lote_postura_base_id SERIAL PRIMARY KEY,
  lote_nombre          VARCHAR(200) NOT NULL,
  codigo_erp           VARCHAR(80) NULL,
  cantidad_hembras     INTEGER NOT NULL DEFAULT 0,
  cantidad_machos      INTEGER NOT NULL DEFAULT 0,
  cantidad_mixtas      INTEGER NOT NULL DEFAULT 0,
  company_id           INTEGER NOT NULL,
  created_by_user_id   INTEGER NOT NULL,
  created_at           TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
  updated_by_user_id   INTEGER NULL,
  updated_at           TIMESTAMP WITHOUT TIME ZONE NULL,
  deleted_at           TIMESTAMP WITHOUT TIME ZONE NULL,
  pais_id              INTEGER NULL,
  CONSTRAINT ck_lpb_nonneg_counts CHECK (cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0)
);

CREATE INDEX IF NOT EXISTS ix_lote_postura_base_company ON public.lote_postura_base(company_id);
CREATE INDEX IF NOT EXISTS ix_lote_postura_base_codigo_erp ON public.lote_postura_base(codigo_erp);

-- Persistir el lote base en el lote principal (asociación a seguimiento)
ALTER TABLE IF EXISTS public.lotes
  ADD COLUMN IF NOT EXISTS lote_postura_base_id INTEGER NULL;
CREATE INDEX IF NOT EXISTS ix_lotes_lote_postura_base_id ON public.lotes(lote_postura_base_id);
");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEV] No se pudo bootstrap lote_postura_base: {ex.Message}");
    }
}

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
        // Permiso que la empresa no habilita: es culpa del request, no del servidor. El mensaje ya
        // dice qué permiso y qué hacer, así que va tal cual con 400.
        if (ex is ZooSanMarino.Application.Exceptions.PermisoNoHabilitadoException pex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new { message = pex.Message });
            return;
        }
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";

        // Un DbUpdateException llega con el texto genérico de EF ("An error occurred while saving the
        // entity changes. See the inner exception for details."), que es lo que terminaba en el toast del
        // usuario sin decir nada — el SqlState real nunca salía del servidor (incidente 2026-08-06, el
        // 22001 de tipo_alimento). Se traduce el código de Postgres a algo accionable; si no está mapeado
        // se conserva el mensaje de siempre. El detalle completo sigue yendo al log del servidor.
        var mensaje = ex?.Message ?? "Error interno del servidor.";
        for (var inner = ex?.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is not System.Data.Common.DbException dbEx) continue;
            var descripcion = ErrorPersistenciaCalculos.DescribirErrorSql(dbEx.SqlState);
            if (descripcion is not null) mensaje = descripcion;
            break;
        }

        await ctx.Response.WriteAsJsonAsync(new { message = mensaje });
    });
});

// 14.0a Compresión de respuesta. Va antes que todo lo que escribe cuerpo para que alcance
// también a las respuestas de error y a las de los endpoints mapeados sin controller.
app.UseResponseCompression();

// 14.0b Routing y CORS (deben ir primero para manejar preflight OPTIONS)
app.UseRouting();
app.UseCors("AppCors");

// ===== Middleware de seguridad (orden importante) =====

// 1. Headers de seguridad HTTP (debe ir temprano)
app.UseSecurityHeaders();

// 2. Rate Limiting (proteger contra DDoS y fuerza bruta)
app.UseRateLimiting();

// 3. Validar SECRET_UP después de CORS pero antes de Authentication/Authorization
// El middleware ya maneja OPTIONS requests internamente
app.UsePlatformSecret();

// 14.1-14.4 Swagger y UI: SOLO fuera de producción
if (!app.Environment.IsProduction())
{
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
    // .AllowAnonymous() en TODO endpoint bajo /swagger: hay un FallbackPolicy que exige sesion a
    // cualquier endpoint sin el atributo (Program.cs, bloque 11). Sin esto el POST del formulario
    // devolvia 401 y NADIE podia entrar a Swagger — se veia la pantalla de login y la contrasena
    // correcta fallaba igual que la incorrecta. La puerta la hace la contrasena, no el JWT: pedir
    // token para entrar a la documentacion es un circulo (el token se saca desde adentro).
    app.MapGet("/swagger-ui/dark.css", () => Results.Text(swaggerDarkCss, "text/css"))
       .AllowAnonymous()
       .ExcludeFromDescription();

    app.MapPost("/swagger/login", async (HttpContext context, IConfiguration config) =>
    {
        var form = await context.Request.ReadFormAsync();
        var expectedPassword = config[SwaggerPasswordMiddleware.ConfigPassword];
        var cookieName = config[SwaggerPasswordMiddleware.ConfigCookie]
                         ?? SwaggerPasswordMiddleware.CookiePorDefecto;

        // La comparación y la emisión de la cookie viven en un solo lugar cada una
        // (SwaggerAccesoCalculos + SwaggerPasswordMiddleware.EmitirSesion). Antes se repetían acá,
        // y el hash duplicado significaba que tocar un lado dejaba a todos afuera de Swagger.
        if (SwaggerAccesoCalculos.PasswordCorrecta(form["password"].ToString(), expectedPassword))
        {
            SwaggerPasswordMiddleware.EmitirSesion(context, expectedPassword!, cookieName);
            context.Response.Redirect("/swagger");
            return;
        }

        // Escapado: la ñ no es ASCII y Kestrel rechaza un header con bytes fuera de ese rango, así
        // que el redirect crudo tiraba 500. El middleware lee Request.Query, que ya viene decodificado.
        context.Response.Redirect("/swagger?error=" + Uri.EscapeDataString("Contraseña incorrecta"));
    })
    .AllowAnonymous()
    .ExcludeFromDescription();

    // Token para probar desde Swagger. POST /api/Auth/login recibe el cuerpo CIFRADO por el front,
    // así que desde la UI no había forma de conseguir un JWT y el botón Authorize quedaba inútil.
    // Vive fuera de /api y bajo /swagger a propósito: sólo se monta fuera de Production y la
    // contraseña de Swagger lo protege igual que al resto de la documentación.
    app.MapPost("/swagger/token", async (SwaggerTokenRequest req, IAuthService auth) =>
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { message = "email y password son obligatorios" });

        try
        {
            var r = await auth.LoginAsync(
                new ZooSanMarino.Application.DTOs.LoginDto { Email = req.Email, Password = req.Password });
            return Results.Ok(r);
        }
        catch (InvalidOperationException ex)
        {
            // Mismo mensaje y mismo código que /api/Auth/login: este atajo no afloja la
            // autenticación, sólo se saltea el cifrado del cuerpo que hace el front.
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
        }
    })
    .AllowAnonymous()
    .WithTags("00 · Pruebas (solo desarrollo)")
    .WithSummary("Token de prueba (email/password en texto plano)")
    .WithDescription(
        "Devuelve el JWT para pegar en Authorize. Existe porque `POST /api/Auth/login` recibe el " +
        "cuerpo cifrado por el front y no se puede llamar desde esta UI. Sólo se monta fuera de " +
        "Production y está detrás de la contraseña de Swagger. Valida las credenciales igual que " +
        "el login real — no es un bypass.");

    app.UseMiddleware<SwaggerPasswordMiddleware>();

    app.MapGet("/swagger/download", (ISwaggerProvider provider) =>
    {
        var doc = provider.GetSwagger("v1");
        using var sw = new StringWriter();
        var w = new OpenApiJsonWriter(sw);
        doc.SerializeAsV3(w);
        var bytes = Encoding.UTF8.GetBytes(sw.ToString());
        return Results.File(bytes, "application/json", "swagger-v1.json");
    })
    .AllowAnonymous()
    .ExcludeFromDescription();

    // 🔑 Firma de plataforma para el "Try it out".
    //
    // PlatformSecretMiddleware exige X-Secret-Up (AES) en TODO /api/*. Swagger UI no la mandaba,
    // así que hasta el 4-sep-2026 cada "Try it out" devolvía 401 platform-secret y la UI servía
    // sólo para leer. Se calcula una vez al arrancar y la inyecta un interceptor de la UI.
    //
    // No agrega exposición: el front ya lleva el secreto EN CLARO en su bundle (environment.ts),
    // que va a todos los navegadores; acá viaja cifrado, sólo fuera de Production y sólo detrás de
    // la contraseña de Swagger. El gate del backend queda intacto: se sigue exigiendo a todo /api/*.
    string? firmaPlataforma = null;
    var secretUpPlano = app.Configuration["PlatformSecret:SecretUpFrontend"];
    var claveFirma = app.Configuration["PlatformSecret:EncryptionKey"];
    if (!string.IsNullOrWhiteSpace(secretUpPlano) && !string.IsNullOrWhiteSpace(claveFirma))
    {
        using var scopeFirma = app.Services.CreateScope();
        firmaPlataforma = scopeFirma.ServiceProvider
            .GetRequiredService<EncryptionService>()
            .Encrypt(secretUpPlano, claveFirma);
    }

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ItalGranja API v1");
        c.DocumentTitle = "ItalGranja — API Docs";
        c.DisplayRequestDuration();
        c.EnableFilter();
        c.EnableDeepLinking();
        c.DefaultModelExpandDepth(1);
        c.DefaultModelsExpandDepth(-1);
        c.DocExpansion(DocExpansion.List);
        c.InjectStylesheet("/swagger-ui/dark.css");

        if (firmaPlataforma is not null)
        {
            // Base64: no lleva comillas, así que no puede romper el literal de JS.
            c.UseRequestInterceptor(
                $"(req) => {{ req.headers['X-Secret-Up'] = '{firmaPlataforma}'; return req; }}");
        }
    });
}

// Routing, CORS y SECRET_UP ya fueron configurados arriba (líneas 350-357)

app.UseAuthentication();

// Resuelve CompanyId efectivo desde X-Active-Company para toda la app
app.UseMiddleware<ZooSanMarino.API.Infrastructure.ActiveCompanyMiddleware>();

app.UseAuthorization();

// Health (público: lo consume el health check de ECS/ALB — NO debe requerir auth)
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapHealthChecks("/hc").AllowAnonymous();

// Debug endpoints: SOLO fuera de producción (filtran configuración del backend: JWT, connection string).
if (!app.Environment.IsProduction())
{
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
    }).AllowAnonymous();

    app.MapGet("/debug/config/conn", (IConfiguration cfg) =>
    {
        var raw = cfg.GetConnectionString("ZooSanMarinoContext")
               ?? cfg["ConnectionStrings:ZooSanMarinoContext"]
               ?? cfg["ZOO_CONN"];

        var safe = string.IsNullOrEmpty(raw)
            ? ""
            : Regex.Replace(raw, "(Password=)([^;]+)", "$1******", RegexOptions.IgnoreCase);

        return Results.Ok(new { ConnectionString = safe });
    }).AllowAnonymous();
}

// Ping DB (público para monitoreo; no revela el detalle del error al cliente)
app.MapGet("/db-ping", async (ZooSanMarinoContext ctx) =>
{
    try
    {
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.CloseConnectionAsync();
        return Results.Ok(new { status = "ok", db = "reachable" });
    }
    catch
    {
        return Results.Problem("DB unreachable");
    }
}).AllowAnonymous();

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
}).AllowAnonymous();

// robots.txt
app.MapGet("/robots.txt", () =>
{
    var robotsTxt = @"# robots.txt — ItalGranja API
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
// RunMigrations: si no está en configuración, por defecto true en Development / Staging / Production
// (evita depender solo de appsettings ignorados por git). Opt-out explícito: "false".
var runMigrationsRaw = app.Configuration["Database:RunMigrations"];
bool runMigrations = string.IsNullOrWhiteSpace(runMigrationsRaw)
    ? (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsProduction())
    : bool.Parse(runMigrationsRaw.Trim());

bool runSeed = app.Configuration.GetValue<bool>("Database:RunSeed");

if (runMigrations || runSeed)
{
    await app.MigrateAndSeedAsync(runMigrations, runSeed);
}
app.Run();


/// <summary>
/// Credenciales para <c>POST /swagger/token</c>, el atajo de pruebas de la UI de Swagger.
/// En texto plano porque el punto es justamente poder llamarlo a mano: el login real
/// (<c>POST /api/Auth/login</c>) recibe el cuerpo cifrado y no se puede escribir desde ahí.
/// </summary>
public record SwaggerTokenRequest(string Email, string Password);


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
