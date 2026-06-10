using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class DbStudioObjectGrantConfiguration : IEntityTypeConfiguration<DbStudioObjectGrant>
{
    public void Configure(EntityTypeBuilder<DbStudioObjectGrant> e)
    {
        e.ToTable("dbstudio_object_grant", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        e.Property(x => x.SchemaName).HasColumnName("schema_name").HasMaxLength(128).IsRequired();
        e.Property(x => x.ObjectName).HasColumnName("object_name").HasMaxLength(256).IsRequired();

        e.Property(x => x.AccessLevel)
         .HasColumnName("access_level")
         .HasConversion<int>()
         .IsRequired();

        e.Property(x => x.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        e.Property(x => x.GrantedAtUtc).HasColumnName("granted_at_utc").IsRequired();

        e.HasIndex(x => new { x.UserId, x.CompanyId, x.SchemaName, x.ObjectName })
         .IsUnique()
         .HasDatabaseName("ux_dbstudio_grant_user_object");
    }
}
