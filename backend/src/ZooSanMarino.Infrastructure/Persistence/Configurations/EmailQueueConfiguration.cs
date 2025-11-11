// src/ZooSanMarino.Infrastructure/Persistence/Configurations/EmailQueueConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class EmailQueueConfiguration : IEntityTypeConfiguration<EmailQueue>
{
    public void Configure(EntityTypeBuilder<EmailQueue> builder)
    {
        builder.ToTable("email_queue");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ToEmail)
            .HasColumnName("to_email")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Subject)
            .HasColumnName("subject")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Body)
            .HasColumnName("body")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.EmailType)
            .HasColumnName("email_type")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("pending");

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(e => e.ErrorType)
            .HasColumnName("error_type")
            .HasMaxLength(100);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(e => e.MaxRetries)
            .HasColumnName("max_retries")
            .HasDefaultValue(3);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at");

        builder.Property(e => e.FailedAt)
            .HasColumnName("failed_at");

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Índices
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("idx_email_queue_status");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("idx_email_queue_created_at");

        builder.HasIndex(e => e.EmailType)
            .HasDatabaseName("idx_email_queue_email_type");
    }
}


