using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialTypeEnumAndGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaterialType",
                table: "Materials",
                newName: "Grade");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Materials",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Materials");

            migrationBuilder.RenameColumn(
                name: "Grade",
                table: "Materials",
                newName: "MaterialType");
        }
    }
}
