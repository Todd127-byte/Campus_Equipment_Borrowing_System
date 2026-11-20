using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public sealed class BorrowingConfiguration : IEntityTypeConfiguration<Borrowing>
{
    public void Configure(EntityTypeBuilder<Borrowing> builder)
    {
        builder.ToTable("Borrowings", table =>
        {
            table.HasCheckConstraint("CK_Borrowings_ValidStatus", "\"Status\" IN (0, 1)");
            table.HasCheckConstraint(
                "CK_Borrowings_ValidDates",
                "\"ExpectedReturnDate\" > \"DateBorrowed\" AND (\"ActualReturnDate\" IS NULL OR \"ActualReturnDate\" >= \"DateBorrowed\")");
            table.HasCheckConstraint(
                "CK_Borrowings_StatusMatchesReturnDate",
                "(\"Status\" = 0 AND \"ActualReturnDate\" IS NULL) OR (\"Status\" = 1 AND \"ActualReturnDate\" IS NOT NULL)");
        });

        builder.HasKey(borrowing => borrowing.Id);
        builder.Property(borrowing => borrowing.Id).ValueGeneratedNever();
        builder.Property(borrowing => borrowing.CheckoutBatchId).IsRequired();
        builder.Property(borrowing => borrowing.StudentId).IsRequired();
        builder.Property(borrowing => borrowing.EquipmentId).IsRequired();
        builder.Property(borrowing => borrowing.DateBorrowed).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(borrowing => borrowing.ExpectedReturnDate).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(borrowing => borrowing.ActualReturnDate).HasColumnType("timestamp with time zone");
        builder.Property(borrowing => borrowing.Status).HasConversion<int>().IsRequired();

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(borrowing => borrowing.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(borrowing => borrowing.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(borrowing => borrowing.StudentId);
        builder.HasIndex(borrowing => new { borrowing.StudentId, borrowing.Status });
        builder.HasIndex(borrowing => new { borrowing.EquipmentId, borrowing.DateBorrowed });
        builder.HasIndex(borrowing => borrowing.ExpectedReturnDate);
        builder.HasIndex(borrowing => borrowing.CheckoutBatchId);
        builder.HasIndex(borrowing => borrowing.EquipmentId)
            .IsUnique()
            .HasFilter("\"Status\" = 0");
    }
}
