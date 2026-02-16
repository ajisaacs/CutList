using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class MaterialDimensionsTPHtoTPT : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the new TPT tables first (before dropping any columns)
            migrationBuilder.CreateTable(
                name: "AngleDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Leg1 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Leg2 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Thickness = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AngleDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AngleDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Flange = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Web = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlatBarDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Thickness = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlatBarDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlatBarDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IBeamDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    WeightPerFoot = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IBeamDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IBeamDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipeDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NominalSize = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Wall = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Schedule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipeDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipeDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RectangularTubeDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Height = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Wall = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RectangularTubeDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RectangularTubeDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundBarDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Diameter = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundBarDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundBarDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundTubeDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    OuterDiameter = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Wall = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundTubeDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundTubeDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SquareBarDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SquareBarDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SquareBarDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SquareTubeDimensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Wall = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SquareTubeDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SquareTubeDimensions_MaterialDimensions_Id",
                        column: x => x.Id,
                        principalTable: "MaterialDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Migrate existing data from the TPH table into the new TPT tables
            migrationBuilder.Sql(@"
                INSERT INTO RoundBarDimensions (Id, Diameter)
                SELECT Id, ISNULL(Diameter, 0) FROM MaterialDimensions WHERE DimensionType = 'RoundBar';

                INSERT INTO RoundTubeDimensions (Id, OuterDiameter, Wall)
                SELECT Id, ISNULL(OuterDiameter, 0), ISNULL(Wall, 0) FROM MaterialDimensions WHERE DimensionType = 'RoundTube';

                INSERT INTO FlatBarDimensions (Id, Width, Thickness)
                SELECT Id, ISNULL(Width, 0), ISNULL(Thickness, 0) FROM MaterialDimensions WHERE DimensionType = 'FlatBar';

                INSERT INTO SquareBarDimensions (Id, Size)
                SELECT Id, ISNULL(Size, 0) FROM MaterialDimensions WHERE DimensionType = 'SquareBar';

                INSERT INTO SquareTubeDimensions (Id, Size, Wall)
                SELECT Id, ISNULL(Size, 0), ISNULL(Wall, 0) FROM MaterialDimensions WHERE DimensionType = 'SquareTube';

                INSERT INTO RectangularTubeDimensions (Id, Width, Height, Wall)
                SELECT Id, ISNULL(Width, 0), ISNULL(Height, 0), ISNULL(Wall, 0) FROM MaterialDimensions WHERE DimensionType = 'RectangularTube';

                INSERT INTO AngleDimensions (Id, Leg1, Leg2, Thickness)
                SELECT Id, ISNULL(Leg1, 0), ISNULL(Leg2, 0), ISNULL(Thickness, 0) FROM MaterialDimensions WHERE DimensionType = 'Angle';

                INSERT INTO ChannelDimensions (Id, Height, Flange, Web)
                SELECT Id, ISNULL(Height, 0), ISNULL(Flange, 0), ISNULL(Web, 0) FROM MaterialDimensions WHERE DimensionType = 'Channel';

                INSERT INTO IBeamDimensions (Id, Height, WeightPerFoot)
                SELECT Id, ISNULL(Height, 0), ISNULL(WeightPerFoot, 0) FROM MaterialDimensions WHERE DimensionType = 'IBeam';

                INSERT INTO PipeDimensions (Id, NominalSize, Wall, Schedule)
                SELECT Id, ISNULL(NominalSize, 0), Wall, Schedule FROM MaterialDimensions WHERE DimensionType = 'Pipe';
            ");

            // 3. Now drop the old TPH columns and indexes
            migrationBuilder.DropIndex(
                name: "IX_MaterialDimensions_Diameter",
                table: "MaterialDimensions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialDimensions_Height",
                table: "MaterialDimensions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialDimensions_Leg1",
                table: "MaterialDimensions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialDimensions_NominalSize",
                table: "MaterialDimensions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialDimensions_OuterDiameter",
                table: "MaterialDimensions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialDimensions_Size",
                table: "MaterialDimensions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialDimensions_Width",
                table: "MaterialDimensions");

            migrationBuilder.DropColumn(name: "Diameter", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "DimensionType", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Flange", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Height", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Leg1", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Leg2", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "NominalSize", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "OuterDiameter", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Schedule", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Size", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Thickness", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Wall", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Web", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "WeightPerFoot", table: "MaterialDimensions");
            migrationBuilder.DropColumn(name: "Width", table: "MaterialDimensions");

            // 4. Create indexes on the new tables
            migrationBuilder.CreateIndex(name: "IX_AngleDimensions_Leg1", table: "AngleDimensions", column: "Leg1");
            migrationBuilder.CreateIndex(name: "IX_ChannelDimensions_Height", table: "ChannelDimensions", column: "Height");
            migrationBuilder.CreateIndex(name: "IX_FlatBarDimensions_Width", table: "FlatBarDimensions", column: "Width");
            migrationBuilder.CreateIndex(name: "IX_IBeamDimensions_Height", table: "IBeamDimensions", column: "Height");
            migrationBuilder.CreateIndex(name: "IX_PipeDimensions_NominalSize", table: "PipeDimensions", column: "NominalSize");
            migrationBuilder.CreateIndex(name: "IX_RectangularTubeDimensions_Width", table: "RectangularTubeDimensions", column: "Width");
            migrationBuilder.CreateIndex(name: "IX_RoundBarDimensions_Diameter", table: "RoundBarDimensions", column: "Diameter");
            migrationBuilder.CreateIndex(name: "IX_RoundTubeDimensions_OuterDiameter", table: "RoundTubeDimensions", column: "OuterDiameter");
            migrationBuilder.CreateIndex(name: "IX_SquareBarDimensions_Size", table: "SquareBarDimensions", column: "Size");
            migrationBuilder.CreateIndex(name: "IX_SquareTubeDimensions_Size", table: "SquareTubeDimensions", column: "Size");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the TPH columns
            migrationBuilder.AddColumn<string>(name: "DimensionType", table: "MaterialDimensions", type: "nvarchar(21)", maxLength: 21, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<decimal>(name: "Diameter", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Flange", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Height", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Leg1", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Leg2", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "NominalSize", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "OuterDiameter", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Schedule", table: "MaterialDimensions", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Size", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Thickness", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Wall", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Web", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "WeightPerFoot", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Width", table: "MaterialDimensions", type: "decimal(10,4)", precision: 10, scale: 4, nullable: true);

            // Migrate data back to TPH
            migrationBuilder.Sql(@"
                UPDATE md SET DimensionType = 'RoundBar', Diameter = rb.Diameter FROM MaterialDimensions md INNER JOIN RoundBarDimensions rb ON md.Id = rb.Id;
                UPDATE md SET DimensionType = 'RoundTube', OuterDiameter = rt.OuterDiameter, Wall = rt.Wall FROM MaterialDimensions md INNER JOIN RoundTubeDimensions rt ON md.Id = rt.Id;
                UPDATE md SET DimensionType = 'FlatBar', Width = fb.Width, Thickness = fb.Thickness FROM MaterialDimensions md INNER JOIN FlatBarDimensions fb ON md.Id = fb.Id;
                UPDATE md SET DimensionType = 'SquareBar', Size = sb.Size FROM MaterialDimensions md INNER JOIN SquareBarDimensions sb ON md.Id = sb.Id;
                UPDATE md SET DimensionType = 'SquareTube', Size = st.Size, Wall = st.Wall FROM MaterialDimensions md INNER JOIN SquareTubeDimensions st ON md.Id = st.Id;
                UPDATE md SET DimensionType = 'RectangularTube', Width = rt.Width, Height = rt.Height, Wall = rt.Wall FROM MaterialDimensions md INNER JOIN RectangularTubeDimensions rt ON md.Id = rt.Id;
                UPDATE md SET DimensionType = 'Angle', Leg1 = a.Leg1, Leg2 = a.Leg2, Thickness = a.Thickness FROM MaterialDimensions md INNER JOIN AngleDimensions a ON md.Id = a.Id;
                UPDATE md SET DimensionType = 'Channel', Height = c.Height, Flange = c.Flange, Web = c.Web FROM MaterialDimensions md INNER JOIN ChannelDimensions c ON md.Id = c.Id;
                UPDATE md SET DimensionType = 'IBeam', Height = ib.Height, WeightPerFoot = ib.WeightPerFoot FROM MaterialDimensions md INNER JOIN IBeamDimensions ib ON md.Id = ib.Id;
                UPDATE md SET DimensionType = 'Pipe', NominalSize = p.NominalSize, Wall = p.Wall, Schedule = p.Schedule FROM MaterialDimensions md INNER JOIN PipeDimensions p ON md.Id = p.Id;
            ");

            // Drop TPT tables
            migrationBuilder.DropTable(name: "AngleDimensions");
            migrationBuilder.DropTable(name: "ChannelDimensions");
            migrationBuilder.DropTable(name: "FlatBarDimensions");
            migrationBuilder.DropTable(name: "IBeamDimensions");
            migrationBuilder.DropTable(name: "PipeDimensions");
            migrationBuilder.DropTable(name: "RectangularTubeDimensions");
            migrationBuilder.DropTable(name: "RoundBarDimensions");
            migrationBuilder.DropTable(name: "RoundTubeDimensions");
            migrationBuilder.DropTable(name: "SquareBarDimensions");
            migrationBuilder.DropTable(name: "SquareTubeDimensions");

            // Re-create TPH indexes
            migrationBuilder.CreateIndex(name: "IX_MaterialDimensions_Diameter", table: "MaterialDimensions", column: "Diameter");
            migrationBuilder.CreateIndex(name: "IX_MaterialDimensions_Height", table: "MaterialDimensions", column: "Height");
            migrationBuilder.CreateIndex(name: "IX_MaterialDimensions_Leg1", table: "MaterialDimensions", column: "Leg1");
            migrationBuilder.CreateIndex(name: "IX_MaterialDimensions_NominalSize", table: "MaterialDimensions", column: "NominalSize");
            migrationBuilder.CreateIndex(name: "IX_MaterialDimensions_OuterDiameter", table: "MaterialDimensions", column: "OuterDiameter");
            migrationBuilder.CreateIndex(name: "IX_MaterialDimensions_Size", table: "MaterialDimensions", column: "Size");
            migrationBuilder.CreateIndex(name: "IX_MaterialDimensions_Width", table: "MaterialDimensions", column: "Width");
        }
    }
}
