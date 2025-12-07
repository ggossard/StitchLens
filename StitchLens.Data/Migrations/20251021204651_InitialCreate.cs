using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StitchLens.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PlanType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YarnBrands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarnBrands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OriginalImagePath = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    MeshCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WidthInches = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    HeightInches = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    MaxColors = table.Column<int>(type: "INTEGER", nullable: false),
                    StitchType = table.Column<string>(type: "TEXT", nullable: false),
                    YarnBrandId = table.Column<int>(type: "INTEGER", nullable: true),
                    PaletteJson = table.Column<string>(type: "TEXT", nullable: true),
                    PdfPath = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Projects_YarnBrands_YarnBrandId",
                        column: x => x.YarnBrandId,
                        principalTable: "YarnBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "YarnColors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YarnBrandId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HexColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Lab_L = table.Column<double>(type: "REAL", nullable: false),
                    Lab_A = table.Column<double>(type: "REAL", nullable: false),
                    Lab_B = table.Column<double>(type: "REAL", nullable: false),
                    YardsPerSkein = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YarnColors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YarnColors_YarnBrands_YarnBrandId",
                        column: x => x.YarnBrandId,
                        principalTable: "YarnBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UserId",
                table: "Projects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_YarnBrandId",
                table: "Projects",
                column: "YarnBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YarnColors_YarnBrandId",
                table: "YarnColors",
                column: "YarnBrandId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "YarnColors");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "YarnBrands");
        }
    }
}
