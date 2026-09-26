using EKitap.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EKitap.Api.Data.Configurations;

public sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Kitaplar", table =>
        {
            table.HasCheckConstraint("CK_Kitaplar_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_Kitaplar_Status", "[Status] IN ('Pending', 'Processing', 'Completed', 'Failed')");
        });
        builder.HasKey(book => book.Id);
        builder.Property(book => book.Id).ValueGeneratedNever();
        builder.Property(book => book.Name).HasMaxLength(200).IsRequired();
        builder.Property(book => book.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(book => book.PdfFilePath).HasMaxLength(1024);
        builder.Property(book => book.ErrorMessage).HasMaxLength(2000);
        builder.HasMany(book => book.Papers)
            .WithOne(paper => paper.Book)
            .HasForeignKey(paper => paper.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
