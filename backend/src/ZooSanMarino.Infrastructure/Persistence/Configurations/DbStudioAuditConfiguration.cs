using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class DbStudioAuditConfiguration : IEntityTypeConfiguration<DbStudioAudit>
{
    public void Configure(EntityTypeBuilder<DbStudioAudit> e)
    {
        e.ToTable("dbstudio_audit", "public");

        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");

        e.Property(x => x.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        e.Property(x => x.SchemaName).HasColumnName("schema_name").HasMaxLength(128);
        e.Property(x => x.ObjectName).HasColumnName("object_name").HasMaxLength(256);
        e.Property(x => x.SqlText).HasColumnName("sql_text").IsRequired();
        e.Property(x => x.ResultSummary).HasColumnName("result_summary").HasColumnType("jsonb").IsRequired();
        e.Property(x => x.Success).HasColumnName("success").IsRequired();

        e.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        e.Property(x => x.ActorEmail).HasColumnName("actor_email").HasMaxLength(256);
        e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);

        e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        e.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("ix_dbstudio_audit_created_at");
        e.HasIndex(x => x.ActorUserId).HasDatabaseName("ix_dbstudio_audit_actor");
    }
}
