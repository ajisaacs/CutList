using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class DimensionsTPTtoTPC : Migration
    {
        private static readonly string[] DimTables =
        [
            "DimAngle", "DimChannel", "DimFlatBar", "DimIBeam", "DimPipe",
            "DimRectangularTube", "DimRoundBar", "DimRoundTube", "DimSquareBar", "DimSquareTube"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop FKs from shape tables to DimBase
            foreach (var table in DimTables)
            {
                migrationBuilder.DropForeignKey(
                    name: $"FK_{table}_DimBase_Id",
                    table: table);
            }

            // 2. Add MaterialId column to each shape table (nullable initially)
            foreach (var table in DimTables)
            {
                migrationBuilder.AddColumn<int>(
                    name: "MaterialId",
                    table: table,
                    type: "int",
                    nullable: true);
            }

            // 3. Copy MaterialId from DimBase into each shape table
            foreach (var table in DimTables)
            {
                migrationBuilder.Sql(
                    $"UPDATE t SET t.MaterialId = b.MaterialId FROM [{table}] t INNER JOIN [DimBase] b ON t.Id = b.Id");
            }

            // 4. Make MaterialId non-nullable now that data is populated
            foreach (var table in DimTables)
            {
                migrationBuilder.AlterColumn<int>(
                    name: "MaterialId",
                    table: table,
                    type: "int",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldNullable: true);
            }

            // 5. Drop DimBase
            migrationBuilder.DropTable(name: "DimBase");

            // 6. Create shared sequence for unique IDs across all shape tables
            migrationBuilder.CreateSequence(name: "MaterialDimensionsSequence");

            // 7. Switch Id columns to use the sequence
            foreach (var table in DimTables)
            {
                migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: table,
                    type: "int",
                    nullable: false,
                    defaultValueSql: "NEXT VALUE FOR [MaterialDimensionsSequence]",
                    oldClrType: typeof(int),
                    oldType: "int");
            }

            // 8. Create indexes and FKs for MaterialId on each shape table
            foreach (var table in DimTables)
            {
                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_MaterialId",
                    table: table,
                    column: "MaterialId",
                    unique: true);

                migrationBuilder.AddForeignKey(
                    name: $"FK_{table}_Materials_MaterialId",
                    table: table,
                    column: "MaterialId",
                    principalTable: "Materials",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FKs and indexes from shape tables
            foreach (var table in DimTables)
            {
                migrationBuilder.DropForeignKey(
                    name: $"FK_{table}_Materials_MaterialId",
                    table: table);

                migrationBuilder.DropIndex(
                    name: $"IX_{table}_MaterialId",
                    table: table);
            }

            // Remove sequence from Id columns
            foreach (var table in DimTables)
            {
                migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: table,
                    type: "int",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "int",
                    oldDefaultValueSql: "NEXT VALUE FOR [MaterialDimensionsSequence]");
            }

            migrationBuilder.DropSequence(name: "MaterialDimensionsSequence");

            // Re-create DimBase
            migrationBuilder.CreateTable(
                name: "DimBase",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimBase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DimBase_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DimBase_MaterialId",
                table: "DimBase",
                column: "MaterialId",
                unique: true);

            // Copy data back to DimBase from all shape tables
            foreach (var table in DimTables)
            {
                migrationBuilder.Sql(
                    $"SET IDENTITY_INSERT [DimBase] ON; INSERT INTO [DimBase] (Id, MaterialId) SELECT Id, MaterialId FROM [{table}]; SET IDENTITY_INSERT [DimBase] OFF;");
            }

            // Re-add FKs from shape tables to DimBase and drop MaterialId
            foreach (var table in DimTables)
            {
                migrationBuilder.AddForeignKey(
                    name: $"FK_{table}_DimBase_Id",
                    table: table,
                    column: "Id",
                    principalTable: "DimBase",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);

                migrationBuilder.DropColumn(name: "MaterialId", table: table);
            }
        }
    }
}
