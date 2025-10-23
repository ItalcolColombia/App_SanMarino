// src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionAvicolaRawConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class ProduccionAvicolaRawConfiguration : IEntityTypeConfiguration<ProduccionAvicolaRaw>
{
    public void Configure(EntityTypeBuilder<ProduccionAvicolaRaw> builder)
    {
        builder.ToTable("produccion_avicola_raw");

        // Clave primaria
        builder.HasKey(x => x.Id);

        // Configuración de propiedades
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AnioGuia)
            .HasColumnName("anio_guia")
            .HasMaxLength(50);

        builder.Property(x => x.Raza)
            .HasColumnName("raza")
            .HasMaxLength(100);

        builder.Property(x => x.Edad)
            .HasColumnName("edad")
            .HasMaxLength(50);

        builder.Property(x => x.MortSemH)
            .HasColumnName("mort_sem_h")
            .HasMaxLength(50);

        builder.Property(x => x.RetiroAcH)
            .HasColumnName("retiro_ac_h")
            .HasMaxLength(50);

        builder.Property(x => x.MortSemM)
            .HasColumnName("mort_sem_m")
            .HasMaxLength(50);

        builder.Property(x => x.RetiroAcM)
            .HasColumnName("retiro_ac_m")
            .HasMaxLength(50);

        builder.Property(x => x.ConsAcH)
            .HasColumnName("cons_ac_h")
            .HasMaxLength(50);

        builder.Property(x => x.ConsAcM)
            .HasColumnName("cons_ac_m")
            .HasMaxLength(50);

        builder.Property(x => x.GrAveDiaH)
            .HasColumnName("gr_ave_dia_h")
            .HasMaxLength(50);

        builder.Property(x => x.GrAveDiaM)
            .HasColumnName("gr_ave_dia_m")
            .HasMaxLength(50);

        builder.Property(x => x.PesoH)
            .HasColumnName("peso_h")
            .HasMaxLength(50);

        builder.Property(x => x.PesoM)
            .HasColumnName("peso_m")
            .HasMaxLength(50);

        builder.Property(x => x.Uniformidad)
            .HasColumnName("uniformidad")
            .HasMaxLength(50);

        builder.Property(x => x.HTotalAa)
            .HasColumnName("h_total_aa")
            .HasMaxLength(50);

        builder.Property(x => x.ProdPorcentaje)
            .HasColumnName("prod_porcentaje")
            .HasMaxLength(50);

        builder.Property(x => x.HIncAa)
            .HasColumnName("h_inc_aa")
            .HasMaxLength(50);

        builder.Property(x => x.AprovSem)
            .HasColumnName("aprov_sem")
            .HasMaxLength(50);

        builder.Property(x => x.PesoHuevo)
            .HasColumnName("peso_huevo")
            .HasMaxLength(50);

        builder.Property(x => x.MasaHuevo)
            .HasColumnName("masa_huevo")
            .HasMaxLength(50);

        builder.Property(x => x.GrasaPorcentaje)
            .HasColumnName("grasa_porcentaje")
            .HasMaxLength(50);

        builder.Property(x => x.NacimPorcentaje)
            .HasColumnName("nacim_porcentaje")
            .HasMaxLength(50);

        builder.Property(x => x.PollitoAa)
            .HasColumnName("pollito_aa")
            .HasMaxLength(50);

        builder.Property(x => x.KcalAveDiaH)
            .HasColumnName("kcal_ave_dia_h")
            .HasMaxLength(50);

        builder.Property(x => x.KcalAveDiaM)
            .HasColumnName("kcal_ave_dia_m")
            .HasMaxLength(50);

        builder.Property(x => x.AprovAc)
            .HasColumnName("aprov_ac")
            .HasMaxLength(50);

        builder.Property(x => x.GrHuevoT)
            .HasColumnName("gr_huevo_t")
            .HasMaxLength(50);

        builder.Property(x => x.GrHuevoInc)
            .HasColumnName("gr_huevo_inc")
            .HasMaxLength(50);

        builder.Property(x => x.GrPollito)
            .HasColumnName("gr_pollito")
            .HasMaxLength(50);

        builder.Property(x => x.Valor1000)
            .HasColumnName("valor_1000")
            .HasMaxLength(50);

        builder.Property(x => x.Valor150)
            .HasColumnName("valor_150")
            .HasMaxLength(50);

        builder.Property(x => x.Apareo)
            .HasColumnName("apareo")
            .HasMaxLength(50);

        builder.Property(x => x.PesoMh)
            .HasColumnName("peso_mh")
            .HasMaxLength(50);

        // Configuración de auditoría (heredada de AuditableEntity)
        builder.Property(x => x.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedByUserId)
            .HasColumnName("updated_by_user_id");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        // Índices para mejorar rendimiento en búsquedas
        builder.HasIndex(x => x.AnioGuia)
            .HasDatabaseName("ix_produccion_avicola_raw_anio_guia");

        builder.HasIndex(x => x.Raza)
            .HasDatabaseName("ix_produccion_avicola_raw_raza");

        builder.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_produccion_avicola_raw_company_id");
    }
}
