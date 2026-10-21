using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessor.Infrastructure.Persistence;

internal sealed class DocumentRecordConfiguration
    : IEntityTypeConfiguration<DocumentRecord>
{
    public void Configure(EntityTypeBuilder<DocumentRecord> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(document => document.Id);

        builder.Property(document => document.FileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(document => document.MediaType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(document => document.Sha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(document => document.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(document => document.AnalysisJson)
            .HasColumnType("jsonb");

        builder.Property(document => document.ProcessingMetadataJson)
            .HasColumnType("jsonb");

        builder.Property(document => document.FailureReason)
            .HasMaxLength(1000);

        builder.HasIndex(document => document.Status);
        builder.HasIndex(document => document.Sha256);
    }
}
