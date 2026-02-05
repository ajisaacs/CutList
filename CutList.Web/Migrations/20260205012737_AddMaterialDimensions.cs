using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterialDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    DimensionType = table.Column<string>(type: "nvarchar(21)", maxLength: 21, nullable: false),
                    Leg1 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Leg2 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Thickness = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Flange = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Web = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    WeightPerFoot = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    NominalSize = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Wall = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Schedule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Diameter = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    OuterDiameter = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Size = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialDimensions_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_Diameter",
                table: "MaterialDimensions",
                column: "Diameter");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_Height",
                table: "MaterialDimensions",
                column: "Height");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_Leg1",
                table: "MaterialDimensions",
                column: "Leg1");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_MaterialId",
                table: "MaterialDimensions",
                column: "MaterialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_NominalSize",
                table: "MaterialDimensions",
                column: "NominalSize");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_OuterDiameter",
                table: "MaterialDimensions",
                column: "OuterDiameter");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_Size",
                table: "MaterialDimensions",
                column: "Size");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDimensions_Width",
                table: "MaterialDimensions",
                column: "Width");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialDimensions");
        }
    }
}
