// src/ZooSanMarino.Infrastructure/Persistence/Configurations/MigracionMasivaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class MigracionMasivaConfiguration : IEntityTypeConfiguration<MigracionMasiva>
{
    public void Configure(EntityTypeBuilder<MigracionMasiva> builder)
    {
        builder.ToTable("migracion_masiva");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Tipo)
            .HasColumnName("tipo")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.NombreArchivo)
            .HasColumnName("nombre_archivo")
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.FilasTotales)
            .HasColumnName("filas_totales")
            .HasDefaultValue(0);

        builder.Property(x => x.FilasProcesadas)
            .HasColumnName("filas_procesadas")
            .HasDefaultValue(0);

        builder.Property(x => x.FilasError)
            .HasColumnName("filas_error")
            .HasDefaultValue(0);

        builder.Property(x => x.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ErroresJson)
            .HasColumnName("errores")
            .HasColumnType("jsonb");

        builder.Property(x => x.FechaProceso)
            .HasColumnName("fecha_proceso")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Auditoría (AuditableEntity)
        builder.Property(x => x.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedByUserId)
            .HasColumnName("updated_by_user_id");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_migracion_masiva_company_id");

        builder.HasIndex(x => x.Tipo)
            .HasDatabaseName("ix_migracion_masiva_tipo");
    }
}
