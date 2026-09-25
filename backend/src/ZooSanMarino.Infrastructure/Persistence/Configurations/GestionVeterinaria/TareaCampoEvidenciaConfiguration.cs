using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class TareaCampoEvidenciaConfiguration : IEntityTypeConfiguration<TareaCampoEvidencia>
{
    public void Configure(EntityTypeBuilder<TareaCampoEvidencia> b)
    {
        b.ToTable("tarea_campo_evidencias", "public");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.TareaId).HasColumnName("tarea_id").IsRequired();
        b.Property(x => x.ImagenBase64).HasColumnName("imagen_base64").IsRequired();
        b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(200);
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(40).IsRequired();
        b.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.HasIndex(x => x.TareaId).HasDatabaseName("ix_tarea_campo_evidencias_tarea");
        b.HasOne(x => x.Tarea).WithMany(x => x.Evidencias).HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
