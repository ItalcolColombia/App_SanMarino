using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class CompanyLogoConfiguration : IEntityTypeConfiguration<CompanyLogo>
{
    public void Configure(EntityTypeBuilder<CompanyLogo> builder)
    {
        builder.ToTable("logo_companias");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.LogoBytes).HasColumnName("logo_bytes").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.LogoContentType).HasColumnName("logo_content_type").HasMaxLength(100).IsRequired();

        builder.HasOne(x => x.Company)
               .WithOne(c => c.Logo)
               .HasForeignKey<CompanyLogo>(x => x.CompanyId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CompanyId).IsUnique();
    }
}
