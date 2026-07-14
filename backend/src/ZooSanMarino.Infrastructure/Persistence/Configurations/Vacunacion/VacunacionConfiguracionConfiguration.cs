// src/ZooSanMarino.Infrastructure/Persistence/Configurations/Vacunacion/VacunacionConfiguracionConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class VacunacionConfiguracionConfiguration : IEntityTypeConfiguration<VacunacionConfiguracion>
{
    public void Configure(EntityTypeBuilder<VacunacionConfiguracion> b)
    {
        b.ToTable("vacunacion_configuracion", schema: "public");
        b.HasKey(x => new { x.CompanyId, x.PaisId });

        b.Property(x => x.CompanyId).HasColumnName("company_id");
        b.Property(x => x.PaisId).HasColumnName("pais_id");
        b.Property(x => x.DiasUmbralIncumplido).HasColumnName("dias_umbral_incumplido").HasDefaultValue(14).IsRequired();

        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");

        b.ToTable(t => t.HasCheckConstraint("ck_vc_umbral_positivo", "dias_umbral_incumplido > 0"));
    }
}
