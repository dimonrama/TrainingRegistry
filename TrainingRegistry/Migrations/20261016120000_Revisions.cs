using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TrainingRegistry.Data;

#nullable disable

namespace TrainingRegistry.Migrations
{
    // доработки по замечаниям заказчика: СНИЛС, документы, ГЗ/ПД, приказы, расширенный журнал и права
    [DbContext(typeof(AppDbContext))]
    [Migration("20261016120000_Revisions")]
    public partial class Revisions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Snils",
                table: "People",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredFields",
                table: "Programs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CommissionMembers",
                table: "Groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionSecretary",
                table: "Groups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CuratorName",
                table: "Groups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingSource",
                table: "Groups",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "Groups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpelReason",
                table: "Enrollments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpelledDate",
                table: "Enrollments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingSource",
                table: "Enrollments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "Enrollments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // существующим пользователям сохраняем прежние возможности, историю им открывает администратор
            migrationBuilder.AddColumn<bool>(
                name: "CanEnroll",
                table: "AppUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewDocuments",
                table: "AppUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanEditDocuments",
                table: "AppUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanReports",
                table: "AppUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewHistory",
                table: "AppUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityTitle",
                table: "AuditLogs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GeneratedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedDocuments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneratedDocuments_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonDocuments_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_People_Snils",
                table: "People",
                column: "Snils");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_RegistrationNumber",
                table: "Groups",
                column: "RegistrationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CertificateNumber",
                table: "Enrollments",
                column: "CertificateNumber");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_GroupId",
                table: "GeneratedDocuments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_PersonId",
                table: "GeneratedDocuments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonDocuments_PersonId_Type",
                table: "PersonDocuments",
                columns: new[] { "PersonId", "Type" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GeneratedDocuments");
            migrationBuilder.DropTable(name: "PersonDocuments");

            migrationBuilder.DropIndex(name: "IX_People_Snils", table: "People");
            migrationBuilder.DropIndex(name: "IX_Groups_RegistrationNumber", table: "Groups");
            migrationBuilder.DropIndex(name: "IX_Enrollments_CertificateNumber", table: "Enrollments");

            migrationBuilder.DropColumn(name: "Snils", table: "People");
            migrationBuilder.DropColumn(name: "RequiredFields", table: "Programs");
            migrationBuilder.DropColumn(name: "CommissionMembers", table: "Groups");
            migrationBuilder.DropColumn(name: "CommissionSecretary", table: "Groups");
            migrationBuilder.DropColumn(name: "CuratorName", table: "Groups");
            migrationBuilder.DropColumn(name: "FundingSource", table: "Groups");
            migrationBuilder.DropColumn(name: "RegistrationNumber", table: "Groups");
            migrationBuilder.DropColumn(name: "ExpelReason", table: "Enrollments");
            migrationBuilder.DropColumn(name: "ExpelledDate", table: "Enrollments");
            migrationBuilder.DropColumn(name: "FundingSource", table: "Enrollments");
            migrationBuilder.DropColumn(name: "Grade", table: "Enrollments");
            migrationBuilder.DropColumn(name: "CanEnroll", table: "AppUsers");
            migrationBuilder.DropColumn(name: "CanViewDocuments", table: "AppUsers");
            migrationBuilder.DropColumn(name: "CanEditDocuments", table: "AppUsers");
            migrationBuilder.DropColumn(name: "CanReports", table: "AppUsers");
            migrationBuilder.DropColumn(name: "CanViewHistory", table: "AppUsers");
            migrationBuilder.DropColumn(name: "Details", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "EntityTitle", table: "AuditLogs");
        }
    }
}
