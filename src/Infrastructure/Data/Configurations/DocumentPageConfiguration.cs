using LargeDocumentProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LargeDocumentProcessing.Infrastructure.Data.Configurations;

public class DocumentPageConfiguration : IEntityTypeConfiguration<DocumentPage>
{
    public void Configure(EntityTypeBuilder<DocumentPage> builder)
    {
        builder.ToTable("DocumentPages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ArtifactStorageKey)
            .HasMaxLength(1024);

        builder.Property(p => p.ThumbnailStorageKey)
            .HasMaxLength(1024);

        builder.Property(p => p.FailureReason)
            .HasMaxLength(2000);

        builder.HasIndex(p => new { p.DocumentId, p.PageNumber })
            .IsUnique();
    }
}
