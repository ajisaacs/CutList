using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddJobNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Jobs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "JobNumber",
                table: "Jobs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Generate job numbers for existing jobs
            migrationBuilder.Sql(@"
                WITH NumberedJobs AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS RowNum
                    FROM Jobs
                )
                UPDATE j
                SET j.JobNumber = 'JOB-' + RIGHT('00000' + CAST(nj.RowNum AS VARCHAR(5)), 5)
                FROM Jobs j
                INNER JOIN NumberedJobs nj ON j.Id = nj.Id
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_JobNumber",
                table: "Jobs",
                column: "JobNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_JobNumber",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "JobNumber",
                table: "Jobs");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Jobs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
