// src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteBaseEngordeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class LoteBaseEngordeConfiguration : IEntityTypeConfiguration<LoteBaseEngorde>
{
    public void Configure(EntityTypeBuilder<LoteBaseEngorde> b)
    {
        b.ToTable("lote_base_engorde", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(80).IsRequired();
        b.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(300);
        b.Property(x => x.CodigoErp).HasColumnName("codigo_erp").HasMaxLength(80);
        b.Property(x => x.LineaGenetica).HasColumnName("linea_genetica").HasMaxLength(120);
        b.Property(x => x.FechaActivacion).HasColumnName("fecha_activacion").HasColumnType("date");
        b.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);

        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_lote_base_engorde_company");
        // Unicidad por empresa (case-insensitive, solo vivos) se refuerza con índice
        // funcional en la migración: ux_lote_base_engorde_company_nombre.
    }
}
