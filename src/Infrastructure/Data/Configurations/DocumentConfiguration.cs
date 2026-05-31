using LargeDocumentProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LargeDocumentProcessing.Infrastructure.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.OwnerUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(d => d.OriginalContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(d => d.OriginalStorageKey)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(d => d.FailureReason)
            .HasMaxLength(2000);

        builder.HasIndex(d => d.OwnerUserId);
        builder.HasIndex(d => d.CreatedAt);

        builder.HasMany(d => d.Pages)
            .WithOne(p => p.Document)
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
