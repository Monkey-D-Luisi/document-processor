using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace DocumentProcessor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.5")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("DocumentProcessor.Infrastructure.Persistence.DocumentRecord", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<string>("AnalysisJson").HasColumnType("jsonb");
            b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<string>("FailureReason").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<string>("FileName").IsRequired().HasMaxLength(260).HasColumnType("character varying(260)");
            b.Property<string>("MediaType").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<string>("ProcessingMetadataJson").HasColumnType("jsonb");
            b.Property<string>("Sha256").IsRequired().IsFixedLength().HasMaxLength(64).HasColumnType("character(64)");
            b.Property<long>("SizeBytes").HasColumnType("bigint");
            b.Property<string>("Status").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
            b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");

            b.HasKey("Id");
            b.HasIndex("Sha256");
            b.HasIndex("Status");
            b.ToTable("documents");
        });
    }
}
