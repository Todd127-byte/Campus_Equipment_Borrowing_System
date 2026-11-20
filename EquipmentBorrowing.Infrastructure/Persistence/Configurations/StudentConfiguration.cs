using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", table =>
        {
            table.HasCheckConstraint("CK_Students_Name_Required", "length(trim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_Students_StudentNumber_Required", "length(trim(\"InstitutionalStudentNumber\")) > 0");
            table.HasCheckConstraint(
                "CK_Students_StudentNumber_Normalized",
                "\"NormalizedStudentNumber\" = upper(trim(\"InstitutionalStudentNumber\"))");
            table.HasCheckConstraint(
                "CK_Students_MaximumActiveBorrowings",
                "\"MaximumActiveBorrowings\" >= 0 AND \"MaximumActiveBorrowings\" <= 3");
        });

        builder.HasKey(student => student.Id);
        builder.Property(student => student.Id).ValueGeneratedOnAdd();
        builder.Property(student => student.InstitutionalStudentNumber).IsRequired().HasMaxLength(40);
        builder.Property(student => student.NormalizedStudentNumber).IsRequired().HasMaxLength(40);
        builder.Property(student => student.Name).IsRequired().HasMaxLength(120);
        builder.Property(student => student.ContactInformation).HasMaxLength(160);
        builder.Property(student => student.IsAllowedToBorrow).IsRequired();
        builder.Property(student => student.MaximumActiveBorrowings).IsRequired();
        builder.HasIndex(student => student.NormalizedStudentNumber).IsUnique();
        builder.HasIndex(student => student.Name);
    }
}
