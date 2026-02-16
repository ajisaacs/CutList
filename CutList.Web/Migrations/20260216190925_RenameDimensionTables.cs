using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameDimensionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AngleDimensions_MaterialDimensions_Id",
                table: "AngleDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChannelDimensions_MaterialDimensions_Id",
                table: "ChannelDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_FlatBarDimensions_MaterialDimensions_Id",
                table: "FlatBarDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_IBeamDimensions_MaterialDimensions_Id",
                table: "IBeamDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialDimensions_Materials_MaterialId",
                table: "MaterialDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_PipeDimensions_MaterialDimensions_Id",
                table: "PipeDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_RectangularTubeDimensions_MaterialDimensions_Id",
                table: "RectangularTubeDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_RoundBarDimensions_MaterialDimensions_Id",
                table: "RoundBarDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_RoundTubeDimensions_MaterialDimensions_Id",
                table: "RoundTubeDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_SquareBarDimensions_MaterialDimensions_Id",
                table: "SquareBarDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_SquareTubeDimensions_MaterialDimensions_Id",
                table: "SquareTubeDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SquareTubeDimensions",
                table: "SquareTubeDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SquareBarDimensions",
                table: "SquareBarDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoundTubeDimensions",
                table: "RoundTubeDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoundBarDimensions",
                table: "RoundBarDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RectangularTubeDimensions",
                table: "RectangularTubeDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PipeDimensions",
                table: "PipeDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MaterialDimensions",
                table: "MaterialDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IBeamDimensions",
                table: "IBeamDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FlatBarDimensions",
                table: "FlatBarDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChannelDimensions",
                table: "ChannelDimensions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AngleDimensions",
                table: "AngleDimensions");

            migrationBuilder.RenameTable(
                name: "SquareTubeDimensions",
                newName: "DimSquareTube");

            migrationBuilder.RenameTable(
                name: "SquareBarDimensions",
                newName: "DimSquareBar");

            migrationBuilder.RenameTable(
                name: "RoundTubeDimensions",
                newName: "DimRoundTube");

            migrationBuilder.RenameTable(
                name: "RoundBarDimensions",
                newName: "DimRoundBar");

            migrationBuilder.RenameTable(
                name: "RectangularTubeDimensions",
                newName: "DimRectangularTube");

            migrationBuilder.RenameTable(
                name: "PipeDimensions",
                newName: "DimPipe");

            migrationBuilder.RenameTable(
                name: "MaterialDimensions",
                newName: "DimBase");

            migrationBuilder.RenameTable(
                name: "IBeamDimensions",
                newName: "DimIBeam");

            migrationBuilder.RenameTable(
                name: "FlatBarDimensions",
                newName: "DimFlatBar");

            migrationBuilder.RenameTable(
                name: "ChannelDimensions",
                newName: "DimChannel");

            migrationBuilder.RenameTable(
                name: "AngleDimensions",
                newName: "DimAngle");

            migrationBuilder.RenameIndex(
                name: "IX_SquareTubeDimensions_Size",
                table: "DimSquareTube",
                newName: "IX_DimSquareTube_Size");

            migrationBuilder.RenameIndex(
                name: "IX_SquareBarDimensions_Size",
                table: "DimSquareBar",
                newName: "IX_DimSquareBar_Size");

            migrationBuilder.RenameIndex(
                name: "IX_RoundTubeDimensions_OuterDiameter",
                table: "DimRoundTube",
                newName: "IX_DimRoundTube_OuterDiameter");

            migrationBuilder.RenameIndex(
                name: "IX_RoundBarDimensions_Diameter",
                table: "DimRoundBar",
                newName: "IX_DimRoundBar_Diameter");

            migrationBuilder.RenameIndex(
                name: "IX_RectangularTubeDimensions_Width",
                table: "DimRectangularTube",
                newName: "IX_DimRectangularTube_Width");

            migrationBuilder.RenameIndex(
                name: "IX_PipeDimensions_NominalSize",
                table: "DimPipe",
                newName: "IX_DimPipe_NominalSize");

            migrationBuilder.RenameIndex(
                name: "IX_MaterialDimensions_MaterialId",
                table: "DimBase",
                newName: "IX_DimBase_MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_IBeamDimensions_Height",
                table: "DimIBeam",
                newName: "IX_DimIBeam_Height");

            migrationBuilder.RenameIndex(
                name: "IX_FlatBarDimensions_Width",
                table: "DimFlatBar",
                newName: "IX_DimFlatBar_Width");

            migrationBuilder.RenameIndex(
                name: "IX_ChannelDimensions_Height",
                table: "DimChannel",
                newName: "IX_DimChannel_Height");

            migrationBuilder.RenameIndex(
                name: "IX_AngleDimensions_Leg1",
                table: "DimAngle",
                newName: "IX_DimAngle_Leg1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimSquareTube",
                table: "DimSquareTube",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimSquareBar",
                table: "DimSquareBar",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimRoundTube",
                table: "DimRoundTube",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimRoundBar",
                table: "DimRoundBar",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimRectangularTube",
                table: "DimRectangularTube",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimPipe",
                table: "DimPipe",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimBase",
                table: "DimBase",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimIBeam",
                table: "DimIBeam",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimFlatBar",
                table: "DimFlatBar",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimChannel",
                table: "DimChannel",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DimAngle",
                table: "DimAngle",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DimAngle_DimBase_Id",
                table: "DimAngle",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimBase_Materials_MaterialId",
                table: "DimBase",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimChannel_DimBase_Id",
                table: "DimChannel",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimFlatBar_DimBase_Id",
                table: "DimFlatBar",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimIBeam_DimBase_Id",
                table: "DimIBeam",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimPipe_DimBase_Id",
                table: "DimPipe",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimRectangularTube_DimBase_Id",
                table: "DimRectangularTube",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimRoundBar_DimBase_Id",
                table: "DimRoundBar",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimRoundTube_DimBase_Id",
                table: "DimRoundTube",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimSquareBar_DimBase_Id",
                table: "DimSquareBar",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DimSquareTube_DimBase_Id",
                table: "DimSquareTube",
                column: "Id",
                principalTable: "DimBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DimAngle_DimBase_Id",
                table: "DimAngle");

            migrationBuilder.DropForeignKey(
                name: "FK_DimBase_Materials_MaterialId",
                table: "DimBase");

            migrationBuilder.DropForeignKey(
                name: "FK_DimChannel_DimBase_Id",
                table: "DimChannel");

            migrationBuilder.DropForeignKey(
                name: "FK_DimFlatBar_DimBase_Id",
                table: "DimFlatBar");

            migrationBuilder.DropForeignKey(
                name: "FK_DimIBeam_DimBase_Id",
                table: "DimIBeam");

            migrationBuilder.DropForeignKey(
                name: "FK_DimPipe_DimBase_Id",
                table: "DimPipe");

            migrationBuilder.DropForeignKey(
                name: "FK_DimRectangularTube_DimBase_Id",
                table: "DimRectangularTube");

            migrationBuilder.DropForeignKey(
                name: "FK_DimRoundBar_DimBase_Id",
                table: "DimRoundBar");

            migrationBuilder.DropForeignKey(
                name: "FK_DimRoundTube_DimBase_Id",
                table: "DimRoundTube");

            migrationBuilder.DropForeignKey(
                name: "FK_DimSquareBar_DimBase_Id",
                table: "DimSquareBar");

            migrationBuilder.DropForeignKey(
                name: "FK_DimSquareTube_DimBase_Id",
                table: "DimSquareTube");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimSquareTube",
                table: "DimSquareTube");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimSquareBar",
                table: "DimSquareBar");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimRoundTube",
                table: "DimRoundTube");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimRoundBar",
                table: "DimRoundBar");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimRectangularTube",
                table: "DimRectangularTube");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimPipe",
                table: "DimPipe");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimIBeam",
                table: "DimIBeam");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimFlatBar",
                table: "DimFlatBar");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimChannel",
                table: "DimChannel");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimBase",
                table: "DimBase");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DimAngle",
                table: "DimAngle");

            migrationBuilder.RenameTable(
                name: "DimSquareTube",
                newName: "SquareTubeDimensions");

            migrationBuilder.RenameTable(
                name: "DimSquareBar",
                newName: "SquareBarDimensions");

            migrationBuilder.RenameTable(
                name: "DimRoundTube",
                newName: "RoundTubeDimensions");

            migrationBuilder.RenameTable(
                name: "DimRoundBar",
                newName: "RoundBarDimensions");

            migrationBuilder.RenameTable(
                name: "DimRectangularTube",
                newName: "RectangularTubeDimensions");

            migrationBuilder.RenameTable(
                name: "DimPipe",
                newName: "PipeDimensions");

            migrationBuilder.RenameTable(
                name: "DimIBeam",
                newName: "IBeamDimensions");

            migrationBuilder.RenameTable(
                name: "DimFlatBar",
                newName: "FlatBarDimensions");

            migrationBuilder.RenameTable(
                name: "DimChannel",
                newName: "ChannelDimensions");

            migrationBuilder.RenameTable(
                name: "DimBase",
                newName: "MaterialDimensions");

            migrationBuilder.RenameTable(
                name: "DimAngle",
                newName: "AngleDimensions");

            migrationBuilder.RenameIndex(
                name: "IX_DimSquareTube_Size",
                table: "SquareTubeDimensions",
                newName: "IX_SquareTubeDimensions_Size");

            migrationBuilder.RenameIndex(
                name: "IX_DimSquareBar_Size",
                table: "SquareBarDimensions",
                newName: "IX_SquareBarDimensions_Size");

            migrationBuilder.RenameIndex(
                name: "IX_DimRoundTube_OuterDiameter",
                table: "RoundTubeDimensions",
                newName: "IX_RoundTubeDimensions_OuterDiameter");

            migrationBuilder.RenameIndex(
                name: "IX_DimRoundBar_Diameter",
                table: "RoundBarDimensions",
                newName: "IX_RoundBarDimensions_Diameter");

            migrationBuilder.RenameIndex(
                name: "IX_DimRectangularTube_Width",
                table: "RectangularTubeDimensions",
                newName: "IX_RectangularTubeDimensions_Width");

            migrationBuilder.RenameIndex(
                name: "IX_DimPipe_NominalSize",
                table: "PipeDimensions",
                newName: "IX_PipeDimensions_NominalSize");

            migrationBuilder.RenameIndex(
                name: "IX_DimIBeam_Height",
                table: "IBeamDimensions",
                newName: "IX_IBeamDimensions_Height");

            migrationBuilder.RenameIndex(
                name: "IX_DimFlatBar_Width",
                table: "FlatBarDimensions",
                newName: "IX_FlatBarDimensions_Width");

            migrationBuilder.RenameIndex(
                name: "IX_DimChannel_Height",
                table: "ChannelDimensions",
                newName: "IX_ChannelDimensions_Height");

            migrationBuilder.RenameIndex(
                name: "IX_DimBase_MaterialId",
                table: "MaterialDimensions",
                newName: "IX_MaterialDimensions_MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_DimAngle_Leg1",
                table: "AngleDimensions",
                newName: "IX_AngleDimensions_Leg1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SquareTubeDimensions",
                table: "SquareTubeDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SquareBarDimensions",
                table: "SquareBarDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoundTubeDimensions",
                table: "RoundTubeDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoundBarDimensions",
                table: "RoundBarDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RectangularTubeDimensions",
                table: "RectangularTubeDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PipeDimensions",
                table: "PipeDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_IBeamDimensions",
                table: "IBeamDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlatBarDimensions",
                table: "FlatBarDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChannelDimensions",
                table: "ChannelDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MaterialDimensions",
                table: "MaterialDimensions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AngleDimensions",
                table: "AngleDimensions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AngleDimensions_MaterialDimensions_Id",
                table: "AngleDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelDimensions_MaterialDimensions_Id",
                table: "ChannelDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FlatBarDimensions_MaterialDimensions_Id",
                table: "FlatBarDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IBeamDimensions_MaterialDimensions_Id",
                table: "IBeamDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialDimensions_Materials_MaterialId",
                table: "MaterialDimensions",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PipeDimensions_MaterialDimensions_Id",
                table: "PipeDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RectangularTubeDimensions_MaterialDimensions_Id",
                table: "RectangularTubeDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoundBarDimensions_MaterialDimensions_Id",
                table: "RoundBarDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoundTubeDimensions_MaterialDimensions_Id",
                table: "RoundTubeDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SquareBarDimensions_MaterialDimensions_Id",
                table: "SquareBarDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SquareTubeDimensions_MaterialDimensions_Id",
                table: "SquareTubeDimensions",
                column: "Id",
                principalTable: "MaterialDimensions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
