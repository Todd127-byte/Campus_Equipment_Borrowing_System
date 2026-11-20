using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentBorrowing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncUpdatedSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Students"
                SET "IsAllowedToBorrow" = 1
                WHERE "Id" = 2 AND "Name" = 'Maria Santos';

                INSERT INTO "Students" ("Id", "Name", "IsAllowedToBorrow", "MaximumActiveBorrowings")
                SELECT 3, 'Ted Balaba', 1, 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Students"
                    WHERE "Id" = 3 OR "Name" = 'Ted Balaba')
                  AND EXISTS (
                    SELECT 1 FROM "Students"
                    WHERE ("Id" = 1 AND "Name" = 'Juan Dela Cruz')
                       OR ("Id" = 2 AND "Name" = 'Maria Santos'));

                UPDATE "Students"
                SET "IsAllowedToBorrow" = 1,
                    "MaximumActiveBorrowings" = 1
                WHERE "Id" = 3 AND "Name" = 'Ted Balaba';

                UPDATE "Equipment"
                SET "IsAvailable" = 1
                WHERE "Id" = 102 AND "Name" = 'Raspberry Pi';

                INSERT INTO "Equipment" ("Id", "Name", "IsAvailable")
                SELECT 105, 'Spare Monitor', 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Equipment"
                    WHERE "Id" = 105 OR "Name" = 'Spare Monitor')
                  AND EXISTS (
                    SELECT 1 FROM "Equipment"
                    WHERE ("Id" = 101 AND "Name" = 'Arduino Uno')
                       OR ("Id" = 102 AND "Name" = 'Raspberry Pi')
                       OR ("Id" = 103 AND "Name" = 'USB Webcam')
                       OR ("Id" = 104 AND "Name" = 'Portable Projector'));

                UPDATE "Equipment"
                SET "IsAvailable" = 1
                WHERE "Id" = 105 AND "Name" = 'Spare Monitor';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep these sample rows and values on downgrade; a user may have
            // created borrowings or otherwise edited them since the migration.
        }
    }
}
