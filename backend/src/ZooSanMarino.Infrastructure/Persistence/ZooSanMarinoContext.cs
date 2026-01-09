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
        public DbSet<Company> Companies { get; set; } = null!;
        public DbSet<Farm> Farms { get; set; } = null!;
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
        public DbSet<MasterList> MasterLists { get; set; } = null!;
        public DbSet<MasterListOption> MasterListOptions { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<RoleCompany> RoleCompanies { get; set; } = null!;
        public DbSet<SeguimientoLoteLevante> SeguimientoLoteLevante { get; set; } = null!;
        public DbSet<SeguimientoProduccion> SeguimientoProduccion { get; set; } = null!;
        public DbSet<ProduccionLote> ProduccionLotes { get; set; } = null!;
        public DbSet<ProduccionSeguimiento> ProduccionSeguimientos { get; set; } = null!;
        // ProduccionDiaria está ignorada del modelo porque SeguimientoProduccion mapea a la misma tabla
        // Si necesitas acceder a ella, usa SeguimientoProduccion en su lugar
        // public DbSet<ProduccionDiaria> ProduccionDiaria { get; set; } = null!;
        public DbSet<Login> Logins { get; set; } = null!;
        public DbSet<UserLogin> UserLogins { get; set; } = null!;
        public DbSet<UserCompany> UserCompanies { get; set; } = null!;
        public DbSet<UserRole> UserRoles { get; set; } = null!;
        public DbSet<UserFarm> UserFarms { get; set; } = null!;
        public DbSet<CompanyPais> CompanyPaises { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<Menu> Menus => Set<Menu>();
        public DbSet<MenuPermission> MenuPermissions => Set<MenuPermission>();
        public DbSet<CatalogItem> CatalogItems { get; set; } = null!;
        public DbSet<SystemConfiguration> SystemConfigurations { get; set; } = null!;
        public DbSet<FarmProductInventory> FarmProductInventory => Set<FarmProductInventory>();
        public DbSet<FarmInventoryMovement> FarmInventoryMovements => Set<FarmInventoryMovement>();
        public DbSet<ProduccionResultadoLevante> ProduccionResultadoLevante => Set<ProduccionResultadoLevante>();
        public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();
        public DbSet<ProduccionAvicolaRaw> ProduccionAvicolaRaw => Set<ProduccionAvicolaRaw>();
        
        // Sistema de Inventario de Aves
        public DbSet<InventarioAves> InventarioAves => Set<InventarioAves>();
        public DbSet<MovimientoAves> MovimientoAves => Set<MovimientoAves>();
        public DbSet<HistorialInventario> HistorialInventario => Set<HistorialInventario>();
        
        // Traslados de Huevos
        public DbSet<TrasladoHuevos> TrasladoHuevos => Set<TrasladoHuevos>();
        
        // Cola de correos electrónicos
        public DbSet<EmailQueue> EmailQueue => Set<EmailQueue>();
        
        // Historial de traslados de lotes
        public DbSet<HistorialTrasladoLote> HistorialTrasladoLote => Set<HistorialTrasladoLote>();
        
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
            
            // Ignorar ProduccionDiaria porque SeguimientoProduccion ya mapea a produccion_diaria
            builder.Ignore<ProduccionDiaria>();
            
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

            // 2) Si se modifican relaciones de User, "tocar" UpdatedAt del User
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added &&
                    entry.State != EntityState.Modified &&
                    entry.State != EntityState.Deleted)
                {
                    continue;
                }

                switch (entry.Entity)
                {
                    case UserRole ur:
                        TouchUserUpdatedAt(ur.UserId, nowDto, nowDt);
                        break;

                    case UserCompany uc:
                        TouchUserUpdatedAt(uc.UserId, nowDto, nowDt);
                        break;

                    case UserLogin ul:
                        TouchUserUpdatedAt(ul.UserId, nowDto, nowDt);
                        break;

                    case UserFarm uf:
                        TouchUserUpdatedAt(uf.UserId, nowDto, nowDt);
                        break;
                }
            }
        }

        private void TouchUserUpdatedAt(Guid userId, DateTimeOffset nowDto, DateTime nowDt)
        {
            // 1) si el User ya está trackeado
            var trackedUserEntry = ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == userId);

            if (trackedUserEntry is not null)
            {
                var meta = trackedUserEntry.Metadata.FindProperty("UpdatedAt");
                if (meta is not null)
                {
                    var p = trackedUserEntry.Property("UpdatedAt");
                    var clr = meta.ClrType;

                    if (clr == typeof(DateTimeOffset) || clr == typeof(DateTimeOffset?))
                        p.CurrentValue = nowDto;
                    else if (clr == typeof(DateTime) || clr == typeof(DateTime?))
                        p.CurrentValue = nowDt;

                    p.IsModified = true; // marca solo la propiedad
                }
                return;
            }

            // 2) Adjuntar proxy ligero si no está trackeado
            var proxy = new User { Id = userId };
            Attach(proxy);

            var entry = Entry(proxy);
            var meta2 = entry.Metadata.FindProperty("UpdatedAt");
            if (meta2 is not null)
            {
                var p = entry.Property("UpdatedAt");
                var clr = meta2.ClrType;

                if (clr == typeof(DateTimeOffset) || clr == typeof(DateTimeOffset?))
                    p.CurrentValue = nowDto;
                else if (clr == typeof(DateTime) || clr == typeof(DateTime?))
                    p.CurrentValue = nowDt;

                p.IsModified = true;
            }
        }
    }
    
    
}
