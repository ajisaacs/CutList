using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixMaterialShapeEnumValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert display names to enum names
            migrationBuilder.Sql(@"
                UPDATE Materials SET Shape = 'RoundBar' WHERE Shape = 'Round Bar';
                UPDATE Materials SET Shape = 'RoundTube' WHERE Shape = 'Round Tube';
                UPDATE Materials SET Shape = 'FlatBar' WHERE Shape = 'Flat Bar';
                UPDATE Materials SET Shape = 'SquareBar' WHERE Shape = 'Square Bar';
                UPDATE Materials SET Shape = 'SquareTube' WHERE Shape = 'Square Tube';
                UPDATE Materials SET Shape = 'RectangularTube' WHERE Shape = 'Rectangular Tube';
                UPDATE Materials SET Shape = 'IBeam' WHERE Shape = 'I-Beam';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
