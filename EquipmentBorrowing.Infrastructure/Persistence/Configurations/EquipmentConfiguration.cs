using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment", table =>
        {
            table.HasCheckConstraint("CK_Equipment_Name_Required", "length(trim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_Equipment_Identifier_Required", "length(trim(\"EquipmentIdentifier\")) > 0");
            table.HasCheckConstraint(
                "CK_Equipment_Identifier_Normalized",
                "\"NormalizedIdentifier\" = upper(trim(\"EquipmentIdentifier\"))");
        });

        builder.HasKey(equipment => equipment.Id);
        builder.Property(equipment => equipment.Id).ValueGeneratedOnAdd();
        builder.Property(equipment => equipment.EquipmentIdentifier).IsRequired().HasMaxLength(80);
        builder.Property(equipment => equipment.NormalizedIdentifier).IsRequired().HasMaxLength(80);
        builder.Property(equipment => equipment.Name).IsRequired().HasMaxLength(160);
        builder.Property(equipment => equipment.Description).HasMaxLength(500);
        builder.Property(equipment => equipment.IsAvailable).IsRequired();
        builder.HasIndex(equipment => equipment.NormalizedIdentifier).IsUnique();
        builder.HasIndex(equipment => equipment.IsAvailable);
        builder.HasIndex(equipment => equipment.Name);
    }
}
