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

            // Clave primaria compuesta: UserId, CompanyId (PaisId opcional, no participa en PK)
            e.HasKey(x => new { x.UserId, x.CompanyId });

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

            // País temporalmente no mapeado para permitir operación sin columna en DB
            e.Ignore(x => x.PaisId);
            e.Ignore(x => x.Pais);

            // Índices para mejorar rendimiento
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CompanyId);
        }
    }
}
