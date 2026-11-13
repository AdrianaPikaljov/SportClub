using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SportClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPassPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PassPlanId",
                table: "MonthlyPasses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PricePaid",
                table: "MonthlyPasses",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchasedAt",
                table: "MonthlyPasses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PassPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Visits = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassPlans", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PassPlans",
                columns: new[] { "Id", "Description", "DurationDays", "IsActive", "Name", "Price", "Visits" },
                values: new object[,]
                {
                    { 1, "Sobib proovimiseks: 4 treeningut kuus.", 30, true, "Proovikaart", 25.0, 4 },
                    { 2, "Kaks treeningut nädalas.", 30, true, "Kuukaart 8", 39.0, 8 },
                    { 3, "Kõige populaarsem: kolm treeningut nädalas.", 30, true, "Kuukaart 12", 49.0, 12 },
                    { 4, "Neile, kes treenivad peaaegu iga päev.", 30, true, "Kuukaart 20", 69.0, 20 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPasses_PassPlanId",
                table: "MonthlyPasses",
                column: "PassPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyPasses_PassPlans_PassPlanId",
                table: "MonthlyPasses",
                column: "PassPlanId",
                principalTable: "PassPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyPasses_PassPlans_PassPlanId",
                table: "MonthlyPasses");

            migrationBuilder.DropTable(
                name: "PassPlans");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyPasses_PassPlanId",
                table: "MonthlyPasses");

            migrationBuilder.DropColumn(
                name: "PassPlanId",
                table: "MonthlyPasses");

            migrationBuilder.DropColumn(
                name: "PricePaid",
                table: "MonthlyPasses");

            migrationBuilder.DropColumn(
                name: "PurchasedAt",
                table: "MonthlyPasses");
        }
    }
}
