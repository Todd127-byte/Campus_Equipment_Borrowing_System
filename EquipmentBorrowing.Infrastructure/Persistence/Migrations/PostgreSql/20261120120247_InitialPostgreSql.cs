using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EquipmentBorrowing.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentIdentifier = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedIdentifier = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.CheckConstraint("CK_Equipment_Identifier_Normalized", "\"NormalizedIdentifier\" = upper(trim(\"EquipmentIdentifier\"))");
                    table.CheckConstraint("CK_Equipment_Identifier_Required", "length(trim(\"EquipmentIdentifier\")) > 0");
                    table.CheckConstraint("CK_Equipment_Name_Required", "length(trim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstitutionalStudentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NormalizedStudentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContactInformation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IsAllowedToBorrow = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumActiveBorrowings = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.CheckConstraint("CK_Students_MaximumActiveBorrowings", "\"MaximumActiveBorrowings\" >= 0 AND \"MaximumActiveBorrowings\" <= 3");
                    table.CheckConstraint("CK_Students_Name_Required", "length(trim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Students_StudentNumber_Normalized", "\"NormalizedStudentNumber\" = upper(trim(\"InstitutionalStudentNumber\"))");
                    table.CheckConstraint("CK_Students_StudentNumber_Required", "length(trim(\"InstitutionalStudentNumber\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Borrowings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    DateBorrowed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Borrowings", x => x.Id);
                    table.CheckConstraint("CK_Borrowings_StatusMatchesReturnDate", "(\"Status\" = 0 AND \"ActualReturnDate\" IS NULL) OR (\"Status\" = 1 AND \"ActualReturnDate\" IS NOT NULL)");
                    table.CheckConstraint("CK_Borrowings_ValidDates", "\"ExpectedReturnDate\" > \"DateBorrowed\" AND (\"ActualReturnDate\" IS NULL OR \"ActualReturnDate\" >= \"DateBorrowed\")");
                    table.CheckConstraint("CK_Borrowings_ValidStatus", "\"Status\" IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_Borrowings_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Borrowings_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_CheckoutBatchId",
                table: "Borrowings",
                column: "CheckoutBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_EquipmentId",
                table: "Borrowings",
                column: "EquipmentId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_EquipmentId_DateBorrowed",
                table: "Borrowings",
                columns: new[] { "EquipmentId", "DateBorrowed" });

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_ExpectedReturnDate",
                table: "Borrowings",
                column: "ExpectedReturnDate");

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_StudentId",
                table: "Borrowings",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_StudentId_Status",
                table: "Borrowings",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_IsAvailable",
                table: "Equipment",
                column: "IsAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Name",
                table: "Equipment",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_NormalizedIdentifier",
                table: "Equipment",
                column: "NormalizedIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_Name",
                table: "Students",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Students_NormalizedStudentNumber",
                table: "Students",
                column: "NormalizedStudentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Borrowings");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "Students");
        }
    }
}
