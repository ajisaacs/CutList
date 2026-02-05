using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingSortOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update SortOrder for existing materials based on their primary dimension
            migrationBuilder.Sql(@"
                UPDATE m
                SET m.SortOrder = CAST(
                    CASE d.DimensionType
                        WHEN 'RoundBar' THEN d.Diameter * 1000
                        WHEN 'RoundTube' THEN d.OuterDiameter * 1000
                        WHEN 'FlatBar' THEN d.Width * 1000
                        WHEN 'SquareBar' THEN d.Size * 1000
                        WHEN 'SquareTube' THEN d.Size * 1000
                        WHEN 'RectangularTube' THEN d.Width * 1000
                        WHEN 'Angle' THEN d.Leg1 * 1000
                        WHEN 'Channel' THEN d.Height * 1000
                        WHEN 'IBeam' THEN d.Height * 1000
                        WHEN 'Pipe' THEN d.NominalSize * 1000
                        ELSE 0
                    END AS INT)
                FROM Materials m
                INNER JOIN MaterialDimensions d ON d.MaterialId = m.Id
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
