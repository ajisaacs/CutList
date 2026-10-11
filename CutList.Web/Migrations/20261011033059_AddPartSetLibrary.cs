using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPartSetLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrawingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DrawingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RevisionKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartSetMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartSetId = table.Column<int>(type: "int", nullable: false),
                    SourceItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    LengthInches = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartSetMembers", x => x.Id);
                    table.CheckConstraint("CK_PartSetMember_Length", "[LengthInches] > 0");
                    table.CheckConstraint("CK_PartSetMember_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_PartSetMembers_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PartSetMembers_PartSets_PartSetId",
                        column: x => x.PartSetId,
                        principalTable: "PartSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartSetMembers_MaterialId",
                table: "PartSetMembers",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PartSetMembers_PartSetId",
                table: "PartSetMembers",
                column: "PartSetId");

            migrationBuilder.CreateIndex(
                name: "IX_PartSets_DrawingKey_RevisionKey",
                table: "PartSets",
                columns: new[] { "DrawingKey", "RevisionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartSetMembers");

            migrationBuilder.DropTable(
                name: "PartSets");
        }
    }
}
