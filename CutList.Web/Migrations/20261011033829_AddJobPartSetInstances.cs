using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPartSetInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartSetInstanceId",
                table: "JobParts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobPartSetInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    PartSetId = table.Column<int>(type: "int", nullable: false),
                    DrawingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPartSetInstances", x => x.Id);
                    table.UniqueConstraint("AK_JobPartSetInstances_Id_JobId", x => new { x.Id, x.JobId });
                    table.CheckConstraint("CK_JobPartSetInstance_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_JobPartSetInstances_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobPartSetInstances_PartSets_PartSetId",
                        column: x => x.PartSetId,
                        principalTable: "PartSets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobParts_PartSetInstanceId_JobId",
                table: "JobParts",
                columns: new[] { "PartSetInstanceId", "JobId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPartSetInstances_JobId",
                table: "JobPartSetInstances",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPartSetInstances_PartSetId",
                table: "JobPartSetInstances",
                column: "PartSetId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobParts_JobPartSetInstances_PartSetInstanceId_JobId",
                table: "JobParts",
                columns: new[] { "PartSetInstanceId", "JobId" },
                principalTable: "JobPartSetInstances",
                principalColumns: new[] { "Id", "JobId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobParts_JobPartSetInstances_PartSetInstanceId_JobId",
                table: "JobParts");

            migrationBuilder.DropTable(
                name: "JobPartSetInstances");

            migrationBuilder.DropIndex(
                name: "IX_JobParts_PartSetInstanceId_JobId",
                table: "JobParts");

            migrationBuilder.DropColumn(
                name: "PartSetInstanceId",
                table: "JobParts");
        }
    }
}
