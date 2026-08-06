// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteEngordeAvesCohorteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

/// <summary>
/// Espejo de <see cref="LoteAvesCohorteConfiguration"/> para la línea de engorde. Mismas convenciones:
/// fechas PURAS (<c>date</c>), soft-delete y FK sin cascada (las cohortes se anulan, no se borran).
/// </summary>
public class LoteEngordeAvesCohorteConfiguration : IEntityTypeConfiguration<LoteEngordeAvesCohorte>
{
    public void Configure(EntityTypeBuilder<LoteEngordeAvesCohorte> b)
    {
        b.ToTable("lote_engorde_aves_cohortes", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        b.Property(x => x.LoteAveEngordeId).HasColumnName("lote_ave_engorde_id").IsRequired();
        b.Property(x => x.LoteAveEngordeOrigenId).HasColumnName("lote_ave_engorde_origen_id");
        b.Property(x => x.MovimientoPolloEngordeId).HasColumnName("movimiento_pollo_engorde_id");

        // Ubicación del origen CONGELADA al momento del traslado.
        b.Property(x => x.GranjaOrigenId).HasColumnName("granja_origen_id");
        b.Property(x => x.NucleoOrigenId).HasColumnName("nucleo_origen_id").HasMaxLength(50);
        b.Property(x => x.GalponOrigenId).HasColumnName("galpon_origen_id").HasMaxLength(50);

        // Fechas PURAS (date): sin componente horario → sin corrimientos de zona horaria.
        b.Property(x => x.FechaIngreso)
            .HasColumnName("fecha_ingreso")
            .HasColumnType("date")
            .IsRequired();
        b.Property(x => x.FechaEncasetCohorte)
            .HasColumnName("fecha_encaset_cohorte")
            .HasColumnType("date")
            .IsRequired();

        b.Property(x => x.CantidadHembras).HasColumnName("cantidad_hembras").HasDefaultValue(0).IsRequired();
        b.Property(x => x.CantidadMachos).HasColumnName("cantidad_machos").HasDefaultValue(0).IsRequired();
        b.Property(x => x.CantidadMixtas).HasColumnName("cantidad_mixtas").HasDefaultValue(0).IsRequired();

        b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(300);

        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        // Propiedad calculada: no es columna.
        b.Ignore(x => x.TotalAves);

        b.HasOne<LoteAveEngorde>()
            .WithMany()
            .HasForeignKey(x => x.LoteAveEngordeId)
            .HasPrincipalKey(l => l.LoteAveEngordeId)
            .HasConstraintName("fk_lote_engorde_aves_cohortes_lote")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.LoteAveEngordeId).HasDatabaseName("ix_lote_engorde_aves_cohortes_lote");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_lote_engorde_aves_cohortes_company");
        b.HasIndex(x => x.MovimientoPolloEngordeId).HasDatabaseName("ix_lote_engorde_aves_cohortes_movimiento");
    }
}
