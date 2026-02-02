using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class MultiMaterialProjectParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Materials_MaterialId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectStockBins");

            migrationBuilder.DropIndex(
                name: "IX_Projects_MaterialId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "Projects");

            migrationBuilder.AlterColumn<string>(
                name: "Customer",
                table: "Projects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "ProjectParts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "MaterialStockLengths",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParts_MaterialId",
                table: "ProjectParts",
                column: "MaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectParts_Materials_MaterialId",
                table: "ProjectParts",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectParts_Materials_MaterialId",
                table: "ProjectParts");

            migrationBuilder.DropIndex(
                name: "IX_ProjectParts_MaterialId",
                table: "ProjectParts");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "ProjectParts");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "MaterialStockLengths");

            migrationBuilder.AlterColumn<string>(
                name: "Customer",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectStockBins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    LengthInches = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStockBins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStockBins_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_MaterialId",
                table: "Projects",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStockBins_ProjectId",
                table: "ProjectStockBins",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Materials_MaterialId",
                table: "Projects",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
