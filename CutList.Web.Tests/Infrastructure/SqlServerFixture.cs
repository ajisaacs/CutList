using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace CutList.Web.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    // Every class in this collection shares one disposable SQL Server database and runs serially;
    // each test resets the data through SqlServerFixture.ResetAsync.
    public const string Name = "SQL Server";
}

/// <summary>
/// Starts a disposable SQL Server container, applies the real ApplicationDbContext migrations to a
/// throwaway database, and hosts CutList.Web against it. No production connection string is used.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // SQL Server 2022 (16.0.4295.3, Ubuntu 22.04), pinned by digest for repeatable runs.
    public const string Image =
        "mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";

    private const string DatabaseName = "CutListLockingTests";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image)
        .WithPassword(CreateEphemeralPassword())
        .Build();

    public CutListWebFactory Factory { get; private set; } = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public IServiceProvider Services => Factory.Services;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
            TrustServerCertificate = true
        }.ConnectionString;

        Factory = new CutListWebFactory(ConnectionString);

        await using var context = await CreateContextAsync();
        var effective = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        var expected = new SqlConnectionStringBuilder(ConnectionString);
        if (effective.DataSource != expected.DataSource || effective.InitialCatalog != DatabaseName)
        {
            throw new InvalidOperationException(
                $"Test host resolved an unexpected database ({effective.DataSource}/{effective.InitialCatalog}); refusing to continue.");
        }

        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory != null)
        {
            await Factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public Task<ApplicationDbContext> CreateContextAsync() =>
        Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    /// <summary>
    /// Clears all job and catalog rows, restores the migration-seeded cutting tools, disarms any
    /// save gate, and seeds one locked and one unlocked job with saved optimization results.
    /// </summary>
    public async Task<TestSeed> ResetAsync()
    {
        Factory.SaveGate.Reset();

        await using var context = await CreateContextAsync();
        await context.Database.ExecuteSqlRawAsync("""
            DELETE FROM JobStocks;
            DELETE FROM JobParts;
            DELETE FROM Jobs;
            DELETE FROM StockItems;
            DELETE FROM DimAngle; DELETE FROM DimChannel; DELETE FROM DimFlatBar; DELETE FROM DimIBeam;
            DELETE FROM DimPipe; DELETE FROM DimRectangularTube; DELETE FROM DimRoundBar; DELETE FROM DimRoundTube;
            DELETE FROM DimSquareBar; DELETE FROM DimSquareTube;
            DELETE FROM Materials;
            DELETE FROM CuttingTools WHERE Id > 4;
            UPDATE CuttingTools SET Name = 'Bandsaw', KerfInches = 0.0625, IsDefault = 1, IsActive = 1 WHERE Id = 1;
            UPDATE CuttingTools SET Name = 'Chop Saw', KerfInches = 0.125, IsDefault = 0, IsActive = 1 WHERE Id = 2;
            UPDATE CuttingTools SET Name = 'Cold Cut Saw', KerfInches = 0.0625, IsDefault = 0, IsActive = 1 WHERE Id = 3;
            UPDATE CuttingTools SET Name = 'Hacksaw', KerfInches = 0.0625, IsDefault = 0, IsActive = 1 WHERE Id = 4;
            """);

        var flatBar = new Material
        {
            Shape = MaterialShape.FlatBar,
            Type = MaterialType.Steel,
            Grade = "A36",
            Size = "1/4 x 2",
            SortOrder = 2000,
            CreatedAt = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc)
        };
        var roundTube = new Material
        {
            Shape = MaterialShape.RoundTube,
            Type = MaterialType.Aluminum,
            Grade = "6061-T6",
            Size = "1-1/2 OD x 0.125 wall",
            SortOrder = 1500,
            CreatedAt = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc)
        };
        context.Materials.AddRange(flatBar, roundTube);
        await context.SaveChangesAsync();

        var flatBarStick = new StockItem
        {
            MaterialId = flatBar.Id,
            LengthInches = 240m,
            Name = "20' stick",
            CreatedAt = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc)
        };
        var roundTubeStick = new StockItem
        {
            MaterialId = roundTube.Id,
            LengthInches = 288m,
            Name = "24' stick",
            CreatedAt = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc)
        };
        context.StockItems.AddRange(flatBarStick, roundTubeStick);
        await context.SaveChangesAsync();

        var lockedAt = new DateTime(2026, 9, 15, 14, 30, 12, DateTimeKind.Utc).AddTicks(1234567);
        var locked = new Job
        {
            JobNumber = "JOB-SEED-LOCKED",
            Name = "Ordered trailer frame",
            Customer = "Locked Customer",
            CuttingToolId = 2,
            Notes = "Materials ordered",
            CreatedAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 14, 16, 45, 0, DateTimeKind.Utc).AddTicks(7654321),
            LockedAt = lockedAt,
            OptimizationResultJson = "{\"seed\":\"locked ordered cut plan\"}",
            OptimizedAt = new DateTime(2026, 9, 14, 16, 50, 0, DateTimeKind.Utc).AddTicks(1111111)
        };
        var unlocked = new Job
        {
            JobNumber = "JOB-SEED-OPEN",
            Name = "Open rack",
            Customer = "Open Customer",
            CuttingToolId = 1,
            Notes = "Still planning",
            CreatedAt = new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc),
            OptimizationResultJson = "{\"seed\":\"unlocked cut plan\"}",
            OptimizedAt = new DateTime(2026, 9, 3, 10, 5, 0, DateTimeKind.Utc)
        };
        context.Jobs.AddRange(locked, unlocked);
        await context.SaveChangesAsync();

        var lockedPart = new JobPart { JobId = locked.Id, MaterialId = flatBar.Id, Name = "Rail", LengthInches = 96.5m, Quantity = 4, SortOrder = 0 };
        var lockedPart2 = new JobPart { JobId = locked.Id, MaterialId = roundTube.Id, Name = "Crossmember", LengthInches = 42.25m, Quantity = 6, SortOrder = 1 };
        var unlockedPart = new JobPart { JobId = unlocked.Id, MaterialId = flatBar.Id, Name = "Shelf brace", LengthInches = 30m, Quantity = 8, SortOrder = 0 };
        context.JobParts.AddRange(lockedPart, lockedPart2, unlockedPart);

        var lockedStock = new JobStock { JobId = locked.Id, MaterialId = flatBar.Id, StockItemId = flatBarStick.Id, LengthInches = 240m, Quantity = 5, IsCustomLength = false, Priority = 3, SortOrder = 0 };
        var lockedCustomStock = new JobStock { JobId = locked.Id, MaterialId = roundTube.Id, StockItemId = null, LengthInches = 150m, Quantity = -1, IsCustomLength = true, Priority = 7, SortOrder = 1 };
        var unlockedStock = new JobStock { JobId = unlocked.Id, MaterialId = flatBar.Id, StockItemId = flatBarStick.Id, LengthInches = 240m, Quantity = 2, IsCustomLength = false, Priority = 10, SortOrder = 0 };
        context.JobStocks.AddRange(lockedStock, lockedCustomStock, unlockedStock);
        await context.SaveChangesAsync();

        return new TestSeed(
            LockedJobId: locked.Id,
            UnlockedJobId: unlocked.Id,
            LockedAt: lockedAt,
            FlatBarMaterialId: flatBar.Id,
            RoundTubeMaterialId: roundTube.Id,
            FlatBarStockItemId: flatBarStick.Id,
            RoundTubeStockItemId: roundTubeStick.Id,
            LockedPartId: lockedPart.Id,
            LockedStockId: lockedStock.Id,
            UnlockedPartId: unlockedPart.Id,
            UnlockedStockId: unlockedStock.Id);
    }

    private static string CreateEphemeralPassword() => $"Aa1!{Guid.NewGuid():N}";
}

public sealed record TestSeed(
    int LockedJobId,
    int UnlockedJobId,
    DateTime LockedAt,
    int FlatBarMaterialId,
    int RoundTubeMaterialId,
    int FlatBarStockItemId,
    int RoundTubeStockItemId,
    int LockedPartId,
    int LockedStockId,
    int UnlockedPartId,
    int UnlockedStockId);
