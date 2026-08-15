// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Implementacion/ImplementacionTareaFirmaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ImplementacionTareaFirmaConfiguration : IEntityTypeConfiguration<ImplementacionTareaFirma>
{
    public void Configure(EntityTypeBuilder<ImplementacionTareaFirma> b)
    {
        b.ToTable("implementacion_tarea_firmas", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.TareaId).HasColumnName("tarea_id").IsRequired();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20)
            .HasDefaultValue("pendiente").IsRequired();

        b.Property(x => x.FirmaTexto).HasColumnName("firma_texto").HasMaxLength(300);
        // PNG base64 del canvas: sin límite de columna (text), acotado en ImplementacionCalculos.
        b.Property(x => x.FirmaImagen).HasColumnName("firma_imagen");
        b.Property(x => x.FirmaTipo).HasColumnName("firma_tipo").HasMaxLength(12);
        b.Property(x => x.ContenidoHash).HasColumnName("contenido_hash").HasMaxLength(64);
        b.Property(x => x.FirmadoUserAgent).HasColumnName("firmado_user_agent").HasMaxLength(400);
        b.Property(x => x.FirmadoIp).HasColumnName("firmado_ip").HasMaxLength(45);
        b.Property(x => x.Nota).HasColumnName("nota").HasMaxLength(2000);
        b.Property(x => x.FechaRespuesta).HasColumnName("fecha_respuesta");

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.TareaId).HasDatabaseName("ix_implementacion_tarea_firmas_tarea");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_implementacion_tarea_firmas_user");
        b.HasIndex(x => x.CompanyId).HasDatabaseName("ix_implementacion_tarea_firmas_company");
        // Un participante por tarea (vivo); las filas soft-deleted no bloquean re-asignar.
        b.HasIndex(x => new { x.TareaId, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_implementacion_tarea_firmas_tarea_user")
            .HasFilter("deleted_at IS NULL");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_implementacion_firma_estado",
                "estado IN ('pendiente', 'firmada', 'rechazada')");
            // NULL permitido: las firmas anteriores a la firma manuscrita y las novedades no lo traen.
            t.HasCheckConstraint(
                "ck_implementacion_firma_tipo",
                "firma_tipo IS NULL OR firma_tipo IN ('digitada', 'manuscrita')");
        });

        b.HasOne(x => x.Tarea).WithMany(t => t.Firmas)
            .HasForeignKey(x => x.TareaId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
