using CutList.Web.Data;
using CutList.Web.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public class PartSetInstanceMigrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task IdempotentSqlInstallsFreshDatabaseAndReplaysWithoutChangingJobs()
    {
        await fixture.ResetAsync();
        await using var migrated = await fixture.CreateContextAsync();
        var migrator = migrated.GetService<IMigrator>();
        var script = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        var expected = migrated.Database.GetMigrations().ToArray();
        var name = "PartSetMigration_" + Guid.NewGuid().ToString("N");
        // Only the fixture's Testcontainers connection is used, never appsettings.
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = "master" };
        await using var master = new SqlConnection(connection.ConnectionString);
        await master.OpenAsync();
        await using (var create = new SqlCommand($"CREATE DATABASE [{name}]", master))
            await create.ExecuteNonQueryAsync();
        try
        {
            connection.InitialCatalog = name;
            await using (var fresh = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connection.ConnectionString).Options))
            {
                await ExecuteScriptAsync(fresh, script);
                Assert.Equal(expected, await fresh.Database.GetAppliedMigrationsAsync());
                Assert.Empty(await fresh.Database.GetPendingMigrationsAsync());
                Assert.Empty(await fresh.JobPartSetInstances.ToListAsync());
                Assert.Empty(await fresh.JobParts.ToListAsync());
                await ExecuteScriptAsync(fresh, script);
                Assert.Equal(expected, await fresh.Database.GetAppliedMigrationsAsync());
            }
            // Replay over populated, already-migrated jobs preserves every snapshot field.
            var before = await JobSnapshot.CaptureAsync(fixture);
            await ExecuteScriptAsync(migrated, script);
            JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
        }
        finally
        {
            await using var drop = new SqlCommand(
                $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", master);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteScriptAsync(ApplicationDbContext context, string script)
    {
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"^GO\s*$",
                     System.Text.RegularExpressions.RegexOptions.Multiline))
            if (!string.IsNullOrWhiteSpace(batch))
                await context.Database.ExecuteSqlRawAsync(batch);
    }
}
