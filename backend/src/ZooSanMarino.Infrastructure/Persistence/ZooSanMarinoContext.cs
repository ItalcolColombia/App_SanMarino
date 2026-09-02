// src/ZooSanMarino.Infrastructure/Persistence/ZooSanMarinoContext.cs
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence.Configurations;

namespace ZooSanMarino.Infrastructure.Persistence
{
    public class ZooSanMarinoContext : DbContext
    {
        public ZooSanMarinoContext(DbContextOptions<ZooSanMarinoContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
        public DbSet<Company> Companies { get; set; } = null!;
        public DbSet<CompanyLogo> CompanyLogos { get; set; } = null!;
        public DbSet<Farm> Farms { get; set; } = null!;
        /// <summary>Catálogo de silos de alimento / bodegas de insumos por granja (no son galpones).</summary>
        public DbSet<FarmSilo> FarmSilos => Set<FarmSilo>();
        /// <summary>Lista maestra de silos por empresa (1..100); de acá se asignan a cada granja.</summary>
        public DbSet<SiloCatalogo> SiloCatalogo => Set<SiloCatalogo>();
        /// <summary>N:M galpón ↔ silo: qué silos alimentan a un galpón (navegación, no contención).</summary>
        public DbSet<GalponSilo> GalponSilos => Set<GalponSilo>();
        /// <summary>N:M lote ↔ silo: de qué silos consume un lote en el seguimiento diario.</summary>
        public DbSet<LoteSilo> LoteSilos => Set<LoteSilo>();

        /// <summary>F7.3 — qué tipos de huevo produce cada lote (lista blanca, solo producción).</summary>
        public DbSet<LoteHuevoItem> LoteHuevoItems => Set<LoteHuevoItem>();
        public DbSet<Nucleo> Nucleos { get; set; } = null!;
        public DbSet<Galpon> Galpones { get; set; } = null!;
        public DbSet<Lote> Lotes { get; set; } = null!;
        public DbSet<LoteReproductora> LoteReproductoras { get; set; } = null!;
        public DbSet<LoteGalpon> LoteGalpones { get; set; } = null!;
        public DbSet<Regional> Regionales { get; set; } = null!;
        public DbSet<Zona> Zonas { get; set; } = null!;
        public DbSet<Pais> Paises { get; set; } = null!;
        public DbSet<Departamento> Departamentos { get; set; } = null!;
        public DbSet<Municipio> Municipios { get; set; } = null!;
        public DbSet<LoteSeguimiento> LoteSeguimientos { get; set; } = null!;
        public DbSet<SeguimientoDiario> SeguimientoDiario { get; set; } = null!;
        public DbSet<MasterList> MasterLists { get; set; } = null!;
        public DbSet<MasterListOption> MasterListOptions { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<RoleCompany> RoleCompanies { get; set; } = null!;
        // Fase 3: tabla deprecada seguimiento_lote_levante retirada (convergencia a
        // seguimiento_diario tipo='levante'). La CLASE SeguimientoLoteLevante se conserva
        // como POCO en memoria (proyección de los liquidadores técnicos), sin DbSet ni mapeo.
        public DbSet<SeguimientoProduccion> SeguimientoProduccion { get; set; } = null!;
        public DbSet<ProduccionLote> ProduccionLotes { get; set; } = null!;
        public DbSet<Login> Logins { get; set; } = null!;
        public DbSet<UserLogin> UserLogins { get; set; } = null!;
        public DbSet<UserCompany> UserCompanies { get; set; } = null!;
        public DbSet<UserRole> UserRoles { get; set; } = null!;
        public DbSet<UserFarm> UserFarms { get; set; } = null!;
        public DbSet<UserFarmScope> UserFarmScopes { get; set; } = null!;
        public DbSet<CompanyPais> CompanyPaises { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<Menu> Menus => Set<Menu>();
        public DbSet<MenuPermission> MenuPermissions => Set<MenuPermission>();
        public DbSet<CatalogItem> CatalogItems { get; set; } = null!;
        public DbSet<SystemConfiguration> SystemConfigurations { get; set; } = null!;
        public DbSet<FarmProductInventory> FarmProductInventory => Set<FarmProductInventory>();
        public DbSet<FarmInventoryMovement> FarmInventoryMovements => Set<FarmInventoryMovement>();
        public DbSet<InventarioGestionStock> InventarioGestionStock => Set<InventarioGestionStock>();
        public DbSet<InventarioGestionMovimiento> InventarioGestionMovimientos => Set<InventarioGestionMovimiento>();
        // Separación (reserva) de un seguimiento diario pendiente de validar: compromete disponible
        // sin tocar stock ni movimientos. Ver SeguimientoReservaAlimento.
        public DbSet<SeguimientoReservaAlimento> SeguimientoReservaAlimento => Set<SeguimientoReservaAlimento>();
        public DbSet<SeguimientoReservaAves> SeguimientoReservaAves => Set<SeguimientoReservaAves>();
        public DbSet<ItemInventario> ItemInventario => Set<ItemInventario>();
        public DbSet<ProduccionResultadoLevante> ProduccionResultadoLevante => Set<ProduccionResultadoLevante>();
        public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();
        public DbSet<CompanyMenu> CompanyMenus => Set<CompanyMenu>();
        public DbSet<CompanyPermission> CompanyPermissions => Set<CompanyPermission>();
        public DbSet<ProduccionAvicolaRaw> ProduccionAvicolaRaw => Set<ProduccionAvicolaRaw>();
        public DbSet<GuiaGeneticaSantaReyes> GuiaGeneticaSantaReyes => Set<GuiaGeneticaSantaReyes>();

        // Idempotencia del push offline (PWA F3): huella por operación capturada sin red.
        public DbSet<SyncOperacion> SyncOperaciones => Set<SyncOperacion>();

        // Guía genética Ecuador (tablas mixto/hembra/macho por empresa + raza + año)
        public DbSet<GuiaGeneticaEcuadorHeader> GuiaGeneticaEcuadorHeader => Set<GuiaGeneticaEcuadorHeader>();
        public DbSet<GuiaGeneticaEcuadorDetalle> GuiaGeneticaEcuadorDetalle => Set<GuiaGeneticaEcuadorDetalle>();
        
        // Sistema de Inventario de Aves
        public DbSet<InventarioAves> InventarioAves => Set<InventarioAves>();
        public DbSet<MovimientoAves> MovimientoAves => Set<MovimientoAves>();
        public DbSet<HistorialInventario> HistorialInventario => Set<HistorialInventario>();
        
        // Traslados de Huevos
        public DbSet<TrasladoHuevos> TrasladoHuevos => Set<TrasladoHuevos>();
        
        // Cola de correos electrónicos
        public DbSet<EmailQueue> EmailQueue => Set<EmailQueue>();

        // Migraciones masivas (auditoría de corridas de carga por Excel)
        public DbSet<MigracionMasiva> MigracionMasiva => Set<MigracionMasiva>();
        
        // Historial de traslados de lotes
        public DbSet<HistorialTrasladoLote> HistorialTrasladoLote => Set<HistorialTrasladoLote>();
        public DbSet<LoteEtapaLevante> LoteEtapaLevante => Set<LoteEtapaLevante>();
        public DbSet<LoteAveEngorde> LoteAveEngorde { get; set; } = null!;
        public DbSet<LoteBaseEngorde> LoteBaseEngorde => Set<LoteBaseEngorde>();
        public DbSet<LoteBaseEngordeGranja> LoteBaseEngordeGranja => Set<LoteBaseEngordeGranja>();
        public DbSet<LoteReproductoraAveEngorde> LoteReproductoraAveEngorde => Set<LoteReproductoraAveEngorde>();
        public DbSet<SeguimientoDiarioAvesEngorde> SeguimientoDiarioAvesEngorde => Set<SeguimientoDiarioAvesEngorde>();
        public DbSet<SeguimientoDiarioLoteReproductoraAvesEngorde> SeguimientoDiarioLoteReproductoraAvesEngorde => Set<SeguimientoDiarioLoteReproductoraAvesEngorde>();
        public DbSet<MovimientoPolloEngorde> MovimientoPolloEngorde => Set<MovimientoPolloEngorde>();
        public DbSet<HistorialLotePolloEngorde> HistorialLotePolloEngorde => Set<HistorialLotePolloEngorde>();
        public DbSet<LiquidacionLoteEngordePanama> LiquidacionLoteEngordePanama => Set<LiquidacionLoteEngordePanama>();

        // Vacunación (cronogramas por lote/granja/galpón)
        public DbSet<VacunacionCronogramaItem> VacunacionCronogramaItem => Set<VacunacionCronogramaItem>();
        public DbSet<VacunacionRegistroAplicacion> VacunacionRegistroAplicacion => Set<VacunacionRegistroAplicacion>();
        public DbSet<VacunacionConfiguracion> VacunacionConfiguracion => Set<VacunacionConfiguracion>();

        /// <summary>Plan de vacunación estándar por empresa/línea/raza, del que se materializa el de cada lote.</summary>
        public DbSet<VacunacionPlanPlantilla> VacunacionPlanPlantilla => Set<VacunacionPlanPlantilla>();
        public DbSet<VacunacionPlanPlantillaItem> VacunacionPlanPlantillaItem => Set<VacunacionPlanPlantillaItem>();
        public DbSet<LoteRegistroHistoricoUnificado> LoteRegistroHistoricoUnificados => Set<LoteRegistroHistoricoUnificado>();
        /// <summary>La atribución del alimento marcado «para el próximo ciclo», persistida como hecho:
        /// la fn diaria la LEE en vez de recalcularla en cada lectura.</summary>
        public DbSet<AlimentoEntregaCicloEngorde> AlimentoEntregaCicloEngorde => Set<AlimentoEntregaCicloEngorde>();
        /// <summary>Cohortes de aves recibidas por traslado (cada grupo conserva la edad de su lote origen).</summary>
        public DbSet<LoteAvesCohorte> LoteAvesCohortes => Set<LoteAvesCohorte>();
        /// <summary>Ídem para la línea de engorde (lote receptor = lote_ave_engorde).</summary>
        public DbSet<LoteEngordeAvesCohorte> LoteEngordeAvesCohortes => Set<LoteEngordeAvesCohorte>();
        public DbSet<LotePosturaLevante> LotePosturaLevante => Set<LotePosturaLevante>();
        public DbSet<LotePosturaProduccion> LotePosturaProduccion => Set<LotePosturaProduccion>();
        public DbSet<LotePosturaBase> LotePosturaBases => Set<LotePosturaBase>();
        public DbSet<HistoricoLotePostura> HistoricoLotePostura => Set<HistoricoLotePostura>();
        public DbSet<EspejoHuevoProduccion> EspejoHuevoProduccion => Set<EspejoHuevoProduccion>();
        public DbSet<LiquidacionCierreLoteLevante> LiquidacionCierreLoteLevante => Set<LiquidacionCierreLoteLevante>();
        public DbSet<LiquidacionLoteEngordeCongelada> LiquidacionLoteEngordeCongelada => Set<LiquidacionLoteEngordeCongelada>();

        // Gastos/consumos de inventario (Ecuador)
        public DbSet<InventarioGasto> InventarioGastos => Set<InventarioGasto>();
        public DbSet<InventarioGastoDetalle> InventarioGastoDetalles => Set<InventarioGastoDetalle>();
        public DbSet<InventarioGastoAuditoria> InventarioGastoAuditorias => Set<InventarioGastoAuditoria>();

        // Gestión de Clientes
        public DbSet<Cliente> Clientes => Set<Cliente>();

        // Lesiones (Panamá — tab dentro de Seguimiento Diario Reproductora/Apoyo/Engorde)
        public DbSet<Lesion> Lesiones => Set<Lesion>();

        // PAT / Tokens de servicio (clientes headless: crones que llaman /api/tickets)
        public DbSet<ServiceToken> ServiceTokens => Set<ServiceToken>();

        // Sesiones de usuario (B1): lista BLANCA por jti. Sin fila no hay sesion (fail-closed).
        public DbSet<SesionActiva> SesionesActivas => Set<SesionActiva>();

        // Módulo de tickets de soporte / requerimientos
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketImagen> TicketImagenes => Set<TicketImagen>();
        public DbSet<TicketAdjunto> TicketAdjuntos => Set<TicketAdjunto>();
        public DbSet<TicketNota> TicketNotas => Set<TicketNota>();
        public DbSet<TicketNotificado> TicketNotificados => Set<TicketNotificado>();
        public DbSet<TicketResolutor> TicketResolutores => Set<TicketResolutor>();
        public DbSet<TicketPerfilUsuario> TicketPerfilesUsuario => Set<TicketPerfilUsuario>();
        public DbSet<TicketResolutorRol> TicketResolutorRoles => Set<TicketResolutorRol>();
        public DbSet<TicketTarea> TicketTareas => Set<TicketTarea>();
        public DbSet<TicketTiempo> TicketTiempos => Set<TicketTiempo>();

        // ItalJira — historias (épicas) que agrupan tareas y casos
        public DbSet<Historia> Historias => Set<Historia>();

        // Módulo Mapas (documentos de mapeo ERP/CIESA)
        public DbSet<Mapa> Mapas => Set<Mapa>();
        public DbSet<MapaPaso> MapaPasos => Set<MapaPaso>();
        public DbSet<MapaEjecucion> MapaEjecuciones => Set<MapaEjecucion>();

        // DB Studio: permisos por objeto + bitácora
        public DbSet<DbStudioObjectGrant> DbStudioObjectGrants => Set<DbStudioObjectGrant>();
        public DbSet<DbStudioAudit> DbStudioAudits => Set<DbStudioAudit>();

        // Módulo Implementación (cronogramas de entrega por empresa con checklist confirmable)
        public DbSet<ImplementacionPlan> ImplementacionPlanes => Set<ImplementacionPlan>();
        public DbSet<ImplementacionTarea> ImplementacionTareas => Set<ImplementacionTarea>();
        public DbSet<ImplementacionTareaFirma> ImplementacionTareaFirmas => Set<ImplementacionTareaFirma>();

        // Nota: Los valores de guía genética se obtienen desde ProduccionAvicolaRaw
        // usando el servicio GuiaGeneticaService basado en Raza y AnoTablaGenetica del lote


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
            // Suprimir warning de cambios pendientes en el modelo
            optionsBuilder.ConfigureWarnings(warnings => 
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfiguration(new RoleMenuConfiguration());

            // Aplica todas las configuraciones IEntityTypeConfiguration<T> automáticamente
            builder.ApplyConfigurationsFromAssembly(typeof(ZooSanMarinoContext).Assembly);
            base.OnModelCreating(builder);
        }

        // ---------------------------
        // SaveChanges con auditoría
        // ---------------------------
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SetDomainDefaults();
            SetAuditFields();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            SetDomainDefaults();
            SetAuditFields();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetDomainDefaults();
            SetAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Defaults de dominio: asegura Unit y Metadata para inventarios/movimientos.
        /// </summary>
        private void SetDomainDefaults()
        {
            foreach (var e in ChangeTracker.Entries<FarmInventoryMovement>())
            {
                if (e.State == EntityState.Added)
                {
                    e.Entity.Unit = string.IsNullOrWhiteSpace(e.Entity.Unit) ? "kg" : e.Entity.Unit.Trim();
                    e.Entity.Metadata ??= JsonDocument.Parse("{}");
                }
            }

            foreach (var e in ChangeTracker.Entries<FarmProductInventory>())
            {
                if (e.State == EntityState.Added || e.State == EntityState.Modified)
                {
                    e.Entity.Unit = string.IsNullOrWhiteSpace(e.Entity.Unit) ? "kg" : e.Entity.Unit.Trim();
                    e.Entity.Metadata ??= e.Entity.Metadata ?? JsonDocument.Parse("{}");
                }
            }
        }

        /// <summary>
        /// Asigna CreatedAt/UpdatedAt respetando el tipo real (DateTime vs DateTimeOffset)
        /// y "toca" UpdatedAt del User cuando cambian relaciones.
        /// </summary>
        private void SetAuditFields()
        {
            var nowDto = DateTimeOffset.UtcNow; // para DateTimeOffset
            var nowDt = DateTime.UtcNow;       // para DateTime

            // 1) CreatedAt / UpdatedAt en entidades
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State is EntityState.Detached or EntityState.Unchanged)
                    continue;

                // CreatedAt solo en Added
                var createdProp = entry.Metadata.FindProperty("CreatedAt");
                if (entry.State == EntityState.Added && createdProp is not null)
                {
                    var p = entry.Property("CreatedAt");
                    var clr = createdProp.ClrType;

                    if (clr == typeof(DateTimeOffset) || clr == typeof(DateTimeOffset?))
                    {
                        if (p.CurrentValue is null || (p.CurrentValue is DateTimeOffset dto && dto == default))
                            p.CurrentValue = nowDto;
                    }
                    else if (clr == typeof(DateTime) || clr == typeof(DateTime?))
                    {
                        if (p.CurrentValue is null || (p.CurrentValue is DateTime dt && dt == default))
                            p.CurrentValue = nowDt;
                    }
                }

                // UpdatedAt en Added o Modified
                var updatedProp = entry.Metadata.FindProperty("UpdatedAt");
                if (updatedProp is not null && (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    var p = entry.Property("UpdatedAt");
                    var clr = updatedProp.ClrType;

                    if (clr == typeof(DateTimeOffset) || clr == typeof(DateTimeOffset?))
                        p.CurrentValue = nowDto;
                    else if (clr == typeof(DateTime) || clr == typeof(DateTime?))
                        p.CurrentValue = nowDt;
                }
            }

            // NOTA: acá vivía un bloque «2)» que, al modificar UserRole/UserCompany/UserLogin/UserFarm,
            // llamaba a un TouchUserUpdatedAt para "tocar" users.updated_at. Se eliminó el 19-ago-2026
            // porque era código muerto que además dejaba basura en el identity map:
            //
            //  · La sombra `UpdatedAt` de `users` está declarada `ValueGeneratedOnAddOrUpdate()` en
            //    `UserConfiguration`, así que EF DESCARTA el valor asignado y nunca emitió un
            //    `UPDATE users`. Medido en la BD local: de 57 filas, 55 tienen `updated_at` a menos de
            //    un segundo de `created_at` y los 2 outliers son un desfase de zona horaria de seeds
            //    (18.000 s = 5 h exactas) ⇒ la columna jamás se actualizó, pese a años de cambios en
            //    `user_farms` y `user_roles`.
            //  · Cuando el User no estaba trackeado hacía `Attach(new User { Id = userId })`, dejando
            //    un proxy HUECO en el identity map: un `Find`/`Include` posterior en el mismo request
            //    podía devolver ese usuario con todos los campos vacíos.
            //
            // Auditoría que lo respalda (repetible): 0 lectores de `EF.Property<…>(u,"UpdatedAt")`,
            // 0 referencias a `users.updated_at` en `backend/sql/` y en el front, 0 triggers sobre
            // `users`.
            //
            // ⛔ La sombra `UpdatedAt` SE CONSERVA mapeada en `UserConfiguration`: la columna existe en
            // local y en prod, y desmapearla haría que la próxima migración de cualquier feature emita
            // un `DropColumn` destructivo.
        }
    }
    
    
}
