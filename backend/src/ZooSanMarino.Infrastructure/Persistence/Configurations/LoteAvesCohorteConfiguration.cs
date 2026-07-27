// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteAvesCohorteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteAvesCohorteConfiguration : IEntityTypeConfiguration<LoteAvesCohorte>
{
    public void Configure(EntityTypeBuilder<LoteAvesCohorte> b)
    {
        b.ToTable("lote_aves_cohortes", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // Lote RECEPTOR de las aves.
        b.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
        b.Property(x => x.LoteOrigenId).HasColumnName("lote_origen_id");
        b.Property(x => x.MovimientoAvesId).HasColumnName("movimiento_aves_id");

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

        b.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(300);

        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        // FK al lote receptor (sin cascada: las cohortes se dan de baja lógicamente).
        b.HasOne<Lote>()
            .WithMany()
            .HasForeignKey(x => x.LoteId)
            .HasPrincipalKey(l => l.LoteId)
            .HasConstraintName("fk_lote_aves_cohortes_lote")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.LoteId).HasDatabaseName("ix_lote_aves_cohortes_lote");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_lote_aves_cohortes_company");
        b.HasIndex(x => x.LoteOrigenId).HasDatabaseName("ix_lote_aves_cohortes_lote_origen");
    }
}
