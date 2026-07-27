using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class UserFarmScopeConfiguration : IEntityTypeConfiguration<UserFarmScope>
{
    public void Configure(EntityTypeBuilder<UserFarmScope> e)
    {
        e.ToTable("user_farm_scopes", t =>
        {
            // Exactamente UN nivel por fila (núcleo | galpón | lote)
            t.HasCheckConstraint(
                "ck_user_farm_scopes_un_nivel",
                "(CASE WHEN nucleo_id IS NOT NULL THEN 1 ELSE 0 END + " +
                "CASE WHEN galpon_id IS NOT NULL THEN 1 ELSE 0 END + " +
                "CASE WHEN lote_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
        });

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        e.Property(x => x.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("integer")
            .IsRequired();

        e.Property(x => x.NucleoId)
            .HasColumnName("nucleo_id")
            .HasMaxLength(64);

        e.Property(x => x.GalponId)
            .HasColumnName("galpon_id")
            .HasMaxLength(64);

        e.Property(x => x.LoteId)
            .HasColumnName("lote_id")
            .HasColumnType("integer");

        e.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        e.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasColumnType("uuid")
            .IsRequired();

        // Asignación usuario-granja dueña de los grants: si se quita la granja, caen los grants.
        e.HasOne(x => x.UserFarm)
            .WithMany(uf => uf.Scopes)
            .HasForeignKey(x => new { x.UserId, x.FarmId })
            .HasConstraintName("fk_user_farm_scopes_user_farms")
            .OnDelete(DeleteBehavior.Cascade);

        // FKs de ubicación en CASCADE: borrar/re-keyear el lugar elimina el grant (fail-closed;
        // las fn_rekey_* hacen copy+DELETE y no deben fallar por RESTRICT).
        e.HasOne<Nucleo>()
            .WithMany()
            .HasForeignKey(x => new { x.NucleoId, x.FarmId })
            .HasPrincipalKey(n => new { n.NucleoId, n.GranjaId })
            .HasConstraintName("fk_user_farm_scopes_nucleos")
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne<Galpon>()
            .WithMany()
            .HasForeignKey(x => x.GalponId)
            .HasConstraintName("fk_user_farm_scopes_galpones")
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne<Lote>()
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .HasConstraintName("fk_user_farm_scopes_lotes")
            .OnDelete(DeleteBehavior.Cascade);

        // Anti-duplicado por nivel
        e.HasIndex(x => new { x.UserId, x.FarmId, x.NucleoId })
            .HasDatabaseName("ux_user_farm_scopes_nucleo")
            .IsUnique()
            .HasFilter("nucleo_id IS NOT NULL");

        e.HasIndex(x => new { x.UserId, x.FarmId, x.GalponId })
            .HasDatabaseName("ux_user_farm_scopes_galpon")
            .IsUnique()
            .HasFilter("galpon_id IS NOT NULL");

        e.HasIndex(x => new { x.UserId, x.FarmId, x.LoteId })
            .HasDatabaseName("ux_user_farm_scopes_lote")
            .IsUnique()
            .HasFilter("lote_id IS NOT NULL");

        e.HasIndex(x => new { x.UserId, x.FarmId })
            .HasDatabaseName("ix_user_farm_scopes_user_farm");
    }
}
