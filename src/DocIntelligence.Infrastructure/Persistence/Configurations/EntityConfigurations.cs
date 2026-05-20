using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DocIntelligence.Domain.Entities;

namespace DocIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// Document entity'sinin EF Core tablo yapılandırması.
/// Fluent API ile kolon tipleri, kısıtlar ve indeksler burada tanımlanır.
/// </summary>
public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.OriginalFileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.OptimizedStoragePath)
            .HasMaxLength(1000);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.FileType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(d => d.Category)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(d => d.ExtractedText)
            .HasColumnType("nvarchar(max)");

        builder.Property(d => d.Tags)
            .HasMaxLength(2000);

        builder.Property(d => d.UploadedByUserId)
            .HasMaxLength(200);

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(2000);

        // İndeksler
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.Category);
        builder.HasIndex(d => d.UploadedByUserId);
        builder.HasIndex(d => d.CreatedAt);

        // İlişkiler
        builder.HasMany(d => d.ProcessingJobs)
            .WithOne(j => j.Document)
            .HasForeignKey(j => j.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// ProcessingJob entity yapılandırması.
/// </summary>
public class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        builder.ToTable("ProcessingJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.JobType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(j => j.ResultMetadata)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(j => j.DocumentId);
        builder.HasIndex(j => j.Status);
    }
}
