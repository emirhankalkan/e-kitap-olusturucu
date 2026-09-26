using EKitap.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EKitap.Api.Data.Configurations;

public sealed class PaperConfiguration : IEntityTypeConfiguration<Paper>
{
    public void Configure(EntityTypeBuilder<Paper> builder)
    {
        builder.ToTable("Bildiriler", table =>
        {
            table.HasCheckConstraint("CK_Bildiriler_SortOrder", "[SortOrder] BETWEEN 1 AND 10");
            table.HasCheckConstraint("CK_Bildiriler_StartPage", "[StartPage] IS NULL OR [StartPage] > 0");
        });
        builder.HasKey(paper => paper.Id);
        builder.Property(paper => paper.Id).ValueGeneratedNever();
        builder.Property(paper => paper.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(paper => paper.StoredFilePath).HasMaxLength(1024).IsRequired();
        builder.Property(paper => paper.Title).HasMaxLength(1000);
        builder.HasIndex(paper => new { paper.BookId, paper.SortOrder }).IsUnique();
    }
}
