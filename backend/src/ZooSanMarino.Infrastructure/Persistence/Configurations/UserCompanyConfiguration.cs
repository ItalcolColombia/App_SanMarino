using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations
{
    public class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
    {
        public void Configure(EntityTypeBuilder<UserCompany> e)
        {
            e.ToTable("user_companies");

            // Clave primaria compuesta: UserId, CompanyId, PaisId
            e.HasKey(x => new { x.UserId, x.CompanyId, x.PaisId });

            // Relación con User
            e.HasOne(x => x.User)
             .WithMany(u => u.UserCompanies)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // Relación con Company
            e.HasOne(x => x.Company)
             .WithMany(c => c.UserCompanies)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);

            // Relación con Pais
            e.HasOne(x => x.Pais)
             .WithMany(p => p.UserCompanies)
             .HasForeignKey(x => x.PaisId)
             .OnDelete(DeleteBehavior.Cascade);

            // Índices para mejorar rendimiento
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.CompanyId, x.PaisId });
        }
    }
}
