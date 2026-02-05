using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixEmptyMaterialType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix empty Type values from when the column was added with defaultValue: ""
            migrationBuilder.Sql("UPDATE Materials SET Type = 'Steel' WHERE Type = '' OR Type IS NULL;");

            // Change the default so new rows also get 'Steel'
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Materials",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Steel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Materials",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
