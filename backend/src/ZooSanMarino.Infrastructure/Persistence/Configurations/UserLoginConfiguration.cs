// UserLoginConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

public class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        // Join table con PK compuesta
        builder.HasKey(x => new { x.UserId, x.LoginId });

        builder.Property(x => x.LockReason).HasMaxLength(500);

        builder.HasOne(x => x.User)
               .WithMany() // o .WithMany(u => u.UserLogins) si existe la colección
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Login)
               .WithMany() // o .WithMany(l => l.UserLogins)
               .HasForeignKey(x => x.LoginId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
