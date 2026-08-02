using CutList.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CutList.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260802212500_RemoveJobNumberPadding")]
    public partial class RemoveJobNumberPadding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Jobs
                SET JobNumber = 'JOB-' + CAST(Id AS varchar(11))
                WHERE JobNumber LIKE 'JOB-%';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Jobs
                SET JobNumber = 'JOB-' + RIGHT('00000' + CAST(Id AS varchar(5)), 5)
                WHERE JobNumber LIKE 'JOB-%';
            ");
        }
    }
}
