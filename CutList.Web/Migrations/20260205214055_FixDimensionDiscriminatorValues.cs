using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixDimensionDiscriminatorValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix discriminator values: database has class names (e.g. 'AngleDimensions')
            // but EF Core config expects short names (e.g. 'Angle')
            migrationBuilder.Sql(@"
                UPDATE MaterialDimensions SET DimensionType = 'Angle' WHERE DimensionType = 'AngleDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'Channel' WHERE DimensionType = 'ChannelDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'FlatBar' WHERE DimensionType = 'FlatBarDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'IBeam' WHERE DimensionType = 'IBeamDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'Pipe' WHERE DimensionType = 'PipeDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'RectangularTube' WHERE DimensionType = 'RectangularTubeDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'RoundBar' WHERE DimensionType = 'RoundBarDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'RoundTube' WHERE DimensionType = 'RoundTubeDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'SquareBar' WHERE DimensionType = 'SquareBarDimensions';
                UPDATE MaterialDimensions SET DimensionType = 'SquareTube' WHERE DimensionType = 'SquareTubeDimensions';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to class name discriminator values
            migrationBuilder.Sql(@"
                UPDATE MaterialDimensions SET DimensionType = 'AngleDimensions' WHERE DimensionType = 'Angle';
                UPDATE MaterialDimensions SET DimensionType = 'ChannelDimensions' WHERE DimensionType = 'Channel';
                UPDATE MaterialDimensions SET DimensionType = 'FlatBarDimensions' WHERE DimensionType = 'FlatBar';
                UPDATE MaterialDimensions SET DimensionType = 'IBeamDimensions' WHERE DimensionType = 'IBeam';
                UPDATE MaterialDimensions SET DimensionType = 'PipeDimensions' WHERE DimensionType = 'Pipe';
                UPDATE MaterialDimensions SET DimensionType = 'RectangularTubeDimensions' WHERE DimensionType = 'RectangularTube';
                UPDATE MaterialDimensions SET DimensionType = 'RoundBarDimensions' WHERE DimensionType = 'RoundBar';
                UPDATE MaterialDimensions SET DimensionType = 'RoundTubeDimensions' WHERE DimensionType = 'RoundTube';
                UPDATE MaterialDimensions SET DimensionType = 'SquareBarDimensions' WHERE DimensionType = 'SquareBar';
                UPDATE MaterialDimensions SET DimensionType = 'SquareTubeDimensions' WHERE DimensionType = 'SquareTube';
            ");
        }
    }
}
