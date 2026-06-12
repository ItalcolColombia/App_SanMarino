using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations
{
    public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
    {
        public void Configure(EntityTypeBuilder<PasswordResetToken> e)
        {
            e.ToTable("password_reset_tokens");

            e.HasKey(t => t.Id);
            e.Property(t => t.Id)
             .HasDefaultValueSql("gen_random_uuid()");

            e.Property(t => t.UserId)
             .IsRequired();

            e.Property(t => t.Token)
             .IsRequired()
             .HasMaxLength(512);

            e.Property(t => t.CreatedAt)
             .HasDefaultValueSql("now()");

            e.Property(t => t.ExpiresAt)
             .IsRequired();

            e.Property(t => t.IsUsed)
             .HasDefaultValue(false);

            e.HasIndex(t => t.Token)
             .HasDatabaseName("ix_password_reset_tokens_token");

            e.HasIndex(t => t.UserId)
             .HasDatabaseName("ix_password_reset_tokens_user_id");

            e.HasIndex(t => t.ExpiresAt)
             .HasDatabaseName("ix_password_reset_tokens_expires_at");

            e.HasOne(t => t.User)
             .WithMany()
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
