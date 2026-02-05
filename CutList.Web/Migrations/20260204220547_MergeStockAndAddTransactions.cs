using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class MergeStockAndAddTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First add the new columns to StockItems
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "StockItems",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityOnHand",
                table: "StockItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Migrate data from MaterialStockLengths to StockItems
            // Update existing StockItems with matching MaterialId + LengthInches
            migrationBuilder.Sql(@"
                UPDATE si
                SET si.QuantityOnHand = msl.Quantity,
                    si.Notes = COALESCE(si.Notes, msl.Notes)
                FROM StockItems si
                INNER JOIN MaterialStockLengths msl
                    ON si.MaterialId = msl.MaterialId
                    AND si.LengthInches = msl.LengthInches
                WHERE msl.IsActive = 1
            ");

            // Insert MaterialStockLengths that don't have a matching StockItem
            migrationBuilder.Sql(@"
                INSERT INTO StockItems (MaterialId, LengthInches, QuantityOnHand, Notes, IsActive, CreatedAt)
                SELECT msl.MaterialId, msl.LengthInches, msl.Quantity, msl.Notes, 1, GETUTCDATE()
                FROM MaterialStockLengths msl
                WHERE msl.IsActive = 1
                AND NOT EXISTS (
                    SELECT 1 FROM StockItems si
                    WHERE si.MaterialId = msl.MaterialId
                    AND si.LengthInches = msl.LengthInches
                )
            ");

            // Now drop the old table
            migrationBuilder.DropTable(
                name: "MaterialStockLengths");

            migrationBuilder.CreateTable(
                name: "StockTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    JobId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransactions_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransactions_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_JobId",
                table: "StockTransactions",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_StockItemId",
                table: "StockTransactions",
                column: "StockItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "QuantityOnHand",
                table: "StockItems");

            migrationBuilder.CreateTable(
                name: "MaterialStockLengths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LengthInches = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialStockLengths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialStockLengths_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialStockLengths_MaterialId_LengthInches",
                table: "MaterialStockLengths",
                columns: new[] { "MaterialId", "LengthInches" },
                unique: true);
        }
    }
}
