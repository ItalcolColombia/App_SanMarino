// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Vacunacion/VacunacionRegistroAplicacionConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class VacunacionRegistroAplicacionConfiguration : IEntityTypeConfiguration<VacunacionRegistroAplicacion>
{
    public void Configure(EntityTypeBuilder<VacunacionRegistroAplicacion> b)
    {
        b.ToTable("vacunacion_registro_aplicacion", schema: "public");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        b.Property(x => x.PaisId).HasColumnName("pais_id");

        b.Property(x => x.VacunacionCronogramaItemId).HasColumnName("vacunacion_cronograma_item_id").IsRequired();

        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
        b.Property(x => x.FechaAplicacion).HasColumnName("fecha_aplicacion").HasColumnType("date");
        b.Property(x => x.DiasDesviacion).HasColumnName("dias_desviacion");
        b.Property(x => x.Incumplido).HasColumnName("incumplido").HasDefaultValue(false).IsRequired();
        b.Property(x => x.MotivoDescripcion).HasColumnName("motivo_descripcion").HasMaxLength(2000);

        b.Property(x => x.UsuarioRegistraId).HasColumnName("usuario_registra_id").IsRequired();
        b.Property(x => x.AplicadoPorUserId).HasColumnName("aplicado_por_user_id");
        b.Property(x => x.AplicadoPorNombreLibre).HasColumnName("aplicado_por_nombre_libre").HasMaxLength(200);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasIndex(x => x.VacunacionCronogramaItemId).IsUnique().HasDatabaseName("ux_vacunacion_registro_aplicacion_item");

        b.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_vra_estado_valido",
                "estado IN ('Pendiente', 'Aplicado', 'AplicadoTardio', 'AplicadoAdelantado', 'NoAplicado')"
            );
            // Motivo obligatorio si no aplicado o si hubo desviación.
            t.HasCheckConstraint(
                "ck_vra_motivo_obligatorio",
                "estado NOT IN ('NoAplicado', 'AplicadoTardio', 'AplicadoAdelantado') OR (motivo_descripcion IS NOT NULL AND length(trim(motivo_descripcion)) > 0)"
            );
            // Aplicado por: FK o texto libre, exactamente uno cuando el estado ya no es Pendiente.
            t.HasCheckConstraint(
                "ck_vra_aplicado_por_coherente",
                "estado = 'Pendiente' OR estado = 'NoAplicado' OR " +
                "((aplicado_por_user_id IS NOT NULL) <> (aplicado_por_nombre_libre IS NOT NULL AND length(trim(aplicado_por_nombre_libre)) > 0))"
            );
        });

        b.HasOne(x => x.VacunacionCronogramaItem)
            .WithOne(x => x.RegistroAplicacion)
            .HasForeignKey<VacunacionRegistroAplicacion>(x => x.VacunacionCronogramaItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
