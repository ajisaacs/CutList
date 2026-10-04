using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class MaterialSizeRepairTests(SqlServerFixture fixture)
{
    private MaterialSizeRepair Service => new(fixture.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private string Evidence => Path.Combine(Environment.GetEnvironmentVariable("TMPDIR") ?? Path.GetTempPath(), "cutlist-task9");

    [Fact]
    public async Task Repairs_same_ids_preserves_catalog_jobs_dimensions_custom_names_and_rolls_back()
    {
        var seed = await SeedAsync();
        var target = await TargetAsync();
        var before = await SnapshotAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        var tube = Assert.Single(plan.Entries, x => x.MaterialId == seed.RoundTubeMaterialId);
        Assert.Equal("repair", tube.Status);
        Assert.Equal("1-1/2\" OD x 1/16\" wall", tube.OldSize);
        Assert.Equal("1-1/2\" OD x 0.083\" wall", tube.NewSize);
        Assert.Contains(plan.Entries, x => x.Shape == "Channel" && x.Status == "repair" && x.NewSize == "3\" x 1-7/16\" x 3/16\"");
        Assert.Contains(plan.Entries, x => x.OldSize == "Authored tube name" && x.Status == "customname");
        Assert.Contains(plan.Entries, x => x.Status == "missingdimensions");
        Assert.Equal(before, await SnapshotAsync());
        MaterialRepairRollback? undo = null;
        var count = await Service.ApplyAsync(plan, target.Server, target.Database, async image =>
        {
            Assert.Equal(before, await SnapshotAsync());
            undo = image;
            Directory.CreateDirectory(Evidence);
            await File.WriteAllTextAsync(Path.Combine(Evidence, "service-before-image.json"), JsonSerializer.Serialize(image, Json));
        });
        Assert.Equal(2, count);
        var after = await JobSnapshot.CaptureAsync(fixture);
        var original = JsonSerializer.Deserialize<SnapshotEnvelope>(before)!;
        Assert.Equal(JsonSerializer.Serialize(original.Catalog.Jobs), JsonSerializer.Serialize(after.Jobs));
        Assert.Equal(JsonSerializer.Serialize(original.Catalog.Parts), JsonSerializer.Serialize(after.Parts));
        Assert.Equal(JsonSerializer.Serialize(original.Catalog.Stock), JsonSerializer.Serialize(after.Stock));
        Assert.Equal(JsonSerializer.Serialize(original.Catalog.StockItems), JsonSerializer.Serialize(after.StockItems));
        await using (var context = await fixture.CreateContextAsync())
        {
            var materials = await context.Materials.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            foreach (var old in original.Catalog.Materials)
            {
                var row = materials.Single(x => x.Id == old.Id);
                var repair = plan.Entries.Single(x => x.MaterialId == old.Id);
                Assert.Equal(repair.NewSize, row.Size);
                row.Size = old.Size;
                row.UpdatedAt = old.UpdatedAt;
            }
            var normalized = materials.Select(x => new MaterialRow(x.Id, x.Shape.ToString(), x.Type.ToString(), x.Grade, x.Size,
                x.Description, x.IsActive, x.SortOrder, x.CreatedAt, x.UpdatedAt));
            Assert.Equal(JsonSerializer.Serialize(original.Catalog.Materials), JsonSerializer.Serialize(normalized));
        }
        Assert.Equal(original.Dimensions, await DimensionsAsync());
        Assert.DoesNotContain((await Service.DryRunAsync(target.Server, target.Database)).Entries, x => x.Status == "repair");
        Assert.NotNull(undo);
        Assert.Equal(2, await Service.RollbackAsync(undo, target.Server, target.Database));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData("server")]
    [InlineData("database")]
    public async Task Wrong_connected_target_rejected_without_writes(string field)
    {
        await SeedAsync();
        var target = await TargetAsync();
        var before = await SnapshotAsync();
        var server = field == "server" ? "not-the-connected-server" : target.Server;
        var database = field == "database" ? "not-the-connected-database" : target.Database;
        await Assert.ThrowsAsync<MaterialRepairRejectedException>(() => Service.DryRunAsync(server, database));
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        await Assert.ThrowsAsync<MaterialRepairRejectedException>(() => Service.ApplyAsync(plan, server, database, _ => Task.CompletedTask));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData("label")]
    [InlineData("timestamp")]
    [InlineData("grade")]
    [InlineData("type")]
    [InlineData("dimension")]
    [InlineData("dimensionid")]
    [InlineData("missingdimensions")]
    public async Task Stale_manifest_aborts_entire_batch(string drift)
    {
        var seed = await SeedAsync();
        var target = await TargetAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        await using (var context = await fixture.CreateContextAsync())
        {
            var row = (await context.Materials.FindAsync(seed.RoundTubeMaterialId))!;
            var dimensions = await context.MaterialDimensions.OfType<RoundTubeDimensions>().SingleAsync(x => x.MaterialId == row.Id);
            switch (drift)
            {
                case "label": row.Size = "Human edit"; break;
                case "timestamp": row.UpdatedAt = row.UpdatedAt!.Value.AddSeconds(1); break;
                case "grade": row.Grade = "new grade"; break;
                case "type": row.Type = MaterialType.Brass; break;
                case "dimension": dimensions.Wall = .095m; break;
                case "dimensionid": context.Remove(dimensions); await context.SaveChangesAsync();
                    context.MaterialDimensions.Add(new RoundTubeDimensions { MaterialId = row.Id, OuterDiameter = 1.5m, Wall = .083m }); break;
                case "missingdimensions": context.Remove(dimensions); break;
            }
            await context.SaveChangesAsync();
        }
        var before = await SnapshotAsync();
        var callbackCalled = false;
        await Assert.ThrowsAsync<MaterialRepairRejectedException>(() => Service.ApplyAsync(plan, target.Server, target.Database, _ =>
        { callbackCalled = true; return Task.CompletedTask; }));
        Assert.False(callbackCalled);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData("newsize")]
    [InlineData("custom")]
    [InlineData("targetguid")]
    [InlineData("duplicate")]
    public async Task Forged_or_malformed_manifest_is_rejected(string tamper)
    {
        await SeedAsync();
        var target = await TargetAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        var index = plan.Entries.FindIndex(x => x.Status == (tamper == "custom" ? "customname" : "repair"));
        if (tamper == "targetguid") plan = plan with { Target = target with { DatabaseGuid = Guid.NewGuid() } };
        else if (tamper == "duplicate") plan.Entries.Add(plan.Entries[index]);
        else plan.Entries[index] = plan.Entries[index] with { Status = "repair", NewSize = "Unreviewed replacement" };
        var before = await SnapshotAsync();
        await Assert.ThrowsAsync<MaterialRepairRejectedException>(() => Service.ApplyAsync(plan, target.Server, target.Database, _ => Task.CompletedTask));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Collision_reported_or_new_collision_rejects_entire_batch(bool afterReview)
    {
        await SeedAsync();
        var target = await TargetAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        await using (var context = await fixture.CreateContextAsync())
        {
            context.Materials.Add(new Material { Shape = MaterialShape.RoundTube, Type = MaterialType.Brass,
                Size = "1-1/2\" od x 0.083\" WALL", IsActive = false });
            await context.SaveChangesAsync();
        }
        if (!afterReview)
        {
            plan = await Service.DryRunAsync(target.Server, target.Database);
            Assert.Contains(plan.Entries, x => x.Status == "collision");
        }
        var before = await SnapshotAsync();
        await Assert.ThrowsAsync<MaterialRepairRejectedException>(() => Service.ApplyAsync(plan, target.Server, target.Database, _ => Task.CompletedTask));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task Failure_after_SQL_writes_rolls_back_every_row_and_keeps_before_image()
    {
        await SeedAsync();
        var target = await TargetAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        var before = await SnapshotAsync();
        MaterialRepairRollback? image = null;
        fixture.Factory.Sql.FailAfterNextBatch(sql => sql.Contains("UPDATE [Materials]"),
            "(SELECT COUNT(*) FROM Materials WHERE Size = N'1-1/2\" OD x 0.083\" wall')");
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => Service.ApplyAsync(plan, target.Server, target.Database, value =>
        { image = value; return Task.CompletedTask; }));
        Assert.Contains("; 1; trancount=", error.InnerException!.Message);
        Assert.NotNull(fixture.Factory.Sql.FaultedCommand);
        Assert.Equal(2, image!.Entries.Count);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task Before_image_failure_prevents_all_writes()
    {
        await SeedAsync();
        var target = await TargetAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        var before = await SnapshotAsync();
        await Assert.ThrowsAsync<IOException>(() => Service.ApplyAsync(plan, target.Server, target.Database,
            _ => throw new IOException("Artifact unavailable")));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData("label")]
    [InlineData("timestamp")]
    [InlineData("dimension")]
    [InlineData("target")]
    [InlineData("collision")]
    public async Task Rollback_drift_or_collision_never_overwrites_human_changes(string drift)
    {
        var seed = await SeedAsync();
        var target = await TargetAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        MaterialRepairRollback? image = null;
        await Service.ApplyAsync(plan, target.Server, target.Database, value => { image = value; return Task.CompletedTask; });
        await using (var context = await fixture.CreateContextAsync())
        {
            var row = (await context.Materials.FindAsync(seed.RoundTubeMaterialId))!;
            if (drift == "label") row.Size = "Human name after repair";
            if (drift == "timestamp") row.UpdatedAt = row.UpdatedAt!.Value.AddSeconds(1);
            if (drift == "dimension") (await context.MaterialDimensions.OfType<RoundTubeDimensions>().SingleAsync()).Wall = .095m;
            if (drift == "collision") context.Materials.Add(new Material { Shape = row.Shape, Size = plan.Entries.Single(x => x.MaterialId == row.Id).OldSize });
            await context.SaveChangesAsync();
        }
        var before = await SnapshotAsync();
        await Assert.ThrowsAsync<MaterialRepairRejectedException>(() => Service.RollbackAsync(image!, drift == "target" ? "other-server" : target.Server, target.Database));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task Square_and_rectangular_generated_labels_repair_without_dimension_changes()
    {
        await SeedAsync();
        await using (var context = await fixture.CreateContextAsync())
        {
            var square = new Material { Shape = MaterialShape.SquareTube, Size = "2\" x 1/16\" wall" };
            var rectangle = new Material { Shape = MaterialShape.RectangularTube, Size = "3\" x 2\" x 1/16\" wall" };
            context.Materials.AddRange(square, rectangle);
            await context.SaveChangesAsync();
            context.MaterialDimensions.AddRange(new SquareTubeDimensions { MaterialId = square.Id, Size = 2m, Wall = .083m },
                new RectangularTubeDimensions { MaterialId = rectangle.Id, Width = 3m, Height = 2m, Wall = .095m });
            await context.SaveChangesAsync();
        }
        var target = await TargetAsync();
        var dimensions = await DimensionsAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        Assert.Contains(plan.Entries, x => x.Shape == "SquareTube" && x.Status == "repair" && x.NewSize == "2\" x 0.083\" wall");
        Assert.Contains(plan.Entries, x => x.Shape == "RectangularTube" && x.Status == "repair" && x.NewSize == "3\" x 2\" x 0.095\" wall");
        Assert.Equal(4, await Service.ApplyAsync(plan, target.Server, target.Database, _ => Task.CompletedTask));
        Assert.Equal(dimensions, await DimensionsAsync());
    }

    [Fact]
    public async Task Two_proposals_with_colliding_new_labels_are_both_blocked()
    {
        await SeedAsync();
        await using (var context = await fixture.CreateContextAsync())
        {
            var material = new Material { Shape = MaterialShape.RoundTube, Size = "1-7/16\" OD x 1/16\" wall" };
            context.Add(material);
            await context.SaveChangesAsync();
            context.MaterialDimensions.Add(new RoundTubeDimensions { MaterialId = material.Id, OuterDiameter = 1.49m, Wall = .083m });
            await context.SaveChangesAsync();
        }
        var target = await TargetAsync();
        var before = await SnapshotAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        Assert.Equal(2, plan.Entries.Count(x => x.Status == "collision"));
        await Assert.ThrowsAsync<MaterialRepairRejectedException>(() => Service.ApplyAsync(plan, target.Server, target.Database, _ => Task.CompletedTask));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData("label")]
    [InlineData("dimensions")]
    [InlineData("collisioninsert")]
    public async Task Serializable_recheck_window_blocks_concurrent_catalog_writes(string mutation)
    {
        var seed = await SeedAsync();
        var target = await TargetAsync();
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        var count = await Service.ApplyAsync(plan, target.Server, target.Database, async _ =>
        {
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = mutation switch
            {
                "label" => "SET LOCK_TIMEOUT 250; UPDATE Materials SET Size = N'Concurrent human edit' WHERE Id = @id",
                "dimensions" => "SET LOCK_TIMEOUT 250; UPDATE DimRoundTube SET Wall = 0.095 WHERE MaterialId = @id",
                _ => "SET LOCK_TIMEOUT 250; INSERT Materials (Shape, Type, Size, IsActive, SortOrder, CreatedAt) VALUES (N'RoundTube', N'Steel', N'1-1/2\" OD x 0.083\" wall', 1, 0, SYSUTCDATETIME())"
            };
            command.Parameters.AddWithValue("@id", seed.RoundTubeMaterialId);
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(1222, error.Number); // deterministic SQL lock timeout, not a sleep-based race
        });
        Assert.Equal(2, count);
        await using var context = await fixture.CreateContextAsync();
        Assert.Equal("1-1/2\" OD x 0.083\" wall", (await context.Materials.FindAsync(seed.RoundTubeMaterialId))!.Size);
        Assert.Equal(.083m, (await context.MaterialDimensions.OfType<RoundTubeDimensions>().SingleAsync()).Wall);
        Assert.Equal(5, await context.Materials.CountAsync());
    }

    [Fact]
    public async Task Real_CLI_durable_manifest_apply_second_run_and_rollback_with_strict_failures()
    {
        await SeedAsync();
        var target = await TargetAsync();
        var before = await SnapshotAsync();
        var directory = Path.Combine(Evidence, "cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var planPath = Path.Combine(directory, "reviewed.json");
        var undoPath = Path.Combine(directory, "before-image.json");
        string[] Common(string server) => ["--connection-env", "CUTLIST_TASK9_FIXTURE_CONNECTION", "--expected-server", server,
            "--expected-database", target.Database];
        async Task<CliResult> Run(string[] arguments, bool secretPresent = true)
        {
            var result = await RunCliAsync(arguments, secretPresent);
            // Only arguments (env *name*, not value), exits and sanitized streams are durable.
            await File.AppendAllTextAsync(Path.Combine(directory, "commands.jsonl"), JsonSerializer.Serialize(result) + "\n");
            return result;
        }
        var dry = await Run([..Common(target.Server), "--manifest", planPath]); // default mode
        Assert.Equal(0, dry.ExitCode);
        var plan = JsonSerializer.Deserialize<MaterialRepairManifest>(await File.ReadAllTextAsync(planPath))!;
        if (OperatingSystem.IsLinux())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(planPath));
        Assert.Equal(target, plan.Target);
        Assert.Equal(2, plan.Entries.Count(x => x.Status == "repair"));
        Assert.Equal(before, await SnapshotAsync());
        foreach (var args in new string[][] {
            [..Common(target.Server), "--dry-run", "--apply", planPath, "--before-image", undoPath],
            [..Common(target.Server), "--manifest", planPath, "--unknown", "Password=never-print-this"],
            ["--manifest", planPath],
            [..Common(target.Server), "--apply", planPath],
            [..Common(target.Server), "--manifest", planPath], // refuses overwrite
            [..Common("not-connected-server"), "--manifest", Path.Combine(directory, "wrong-target.json")],
            [..Common(target.Server), "--manifest", Path.Combine(directory, "absent", "plan.json")],
            [..Common(target.Server), "--connection-env", "duplicate"],
            [..Common(target.Server), "--rollback", planPath],
        })
        {
            var result = await Run(args);
            Assert.NotEqual(0, result.ExitCode);
            Assert.DoesNotContain("never-print-this", result.Stdout + result.Stderr);
            Assert.Equal(before, await SnapshotAsync());
        }
        Assert.NotEqual(0, (await Run([..Common(target.Server), "--manifest", Path.Combine(directory, "no-env.json")], false)).ExitCode);
        var applied = await Run([..Common(target.Server), "--apply", planPath, "--before-image", undoPath]);
        Assert.Equal(0, applied.ExitCode);
        var undo = JsonSerializer.Deserialize<MaterialRepairRollback>(await File.ReadAllTextAsync(undoPath))!;
        if (OperatingSystem.IsLinux())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(undoPath));
        Assert.Equal(2, undo.Entries.Count);
        await using (var context = await fixture.CreateContextAsync())
        {
            foreach (var entry in undo.Entries)
            {
                var row = (await context.Materials.FindAsync(entry.Before.MaterialId))!;
                Assert.Equal(entry.Before.NewSize, row.Size);
                Assert.Equal(entry.AppliedUpdatedAt, row.UpdatedAt);
            }
        }
        var appliedSnapshot = await SnapshotAsync();
        Assert.NotEqual(before, appliedSnapshot);
        Assert.NotEqual(0, (await Run([..Common(target.Server), "--apply", planPath, "--before-image", undoPath])).ExitCode);
        Assert.Equal(appliedSnapshot, await SnapshotAsync());
        var secondPath = Path.Combine(directory, "second.json");
        Assert.Equal(0, (await Run([..Common(target.Server), "--dry-run", "--manifest", secondPath])).ExitCode);
        Assert.DoesNotContain(JsonSerializer.Deserialize<MaterialRepairManifest>(await File.ReadAllTextAsync(secondPath))!.Entries, x => x.Status == "repair");
        Assert.Equal(0, (await Run([..Common(target.Server), "--rollback", undoPath])).ExitCode);
        Assert.Equal(before, await SnapshotAsync());
        await File.WriteAllTextAsync(Path.Combine(directory, "readback.json"), JsonSerializer.Serialize(new { Restored = true, AppliedRows = undo.Entries.Count, Target = target }, Json));
    }

    [Theory]
    [InlineData("inputsymlink")]
    [InlineData("outputsymlink")]
    [InlineData("directorysymlink")]
    [InlineData("noncanonical")]
    [InlineData("hardlink")]
    [InlineData("fifo")]
    public async Task Real_CLI_rejects_unsafe_artifact_paths_without_writes(string unsafePath)
    {
        await SeedAsync();
        var target = await TargetAsync();
        var before = await SnapshotAsync();
        var directory = Path.Combine(Evidence, "unsafe-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var planPath = Path.Combine(directory, "reviewed.json");
        var plan = await Service.DryRunAsync(target.Server, target.Database);
        await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(plan, Json));
        var imagePath = Path.Combine(directory, "before-image.json");
        if (unsafePath == "inputsymlink")
        {
            var link = Path.Combine(directory, "reviewed-link.json");
            File.CreateSymbolicLink(link, planPath);
            planPath = link;
        }
        else if (unsafePath == "outputsymlink") File.CreateSymbolicLink(imagePath, planPath);
        else if (unsafePath == "directorysymlink")
        {
            var link = Path.Combine(directory, "linked-directory");
            Directory.CreateSymbolicLink(link, directory);
            imagePath = Path.Combine(link, "before-image.json");
        }
        else if (unsafePath is "hardlink" or "fifo")
        {
            var inputPath = Path.Combine(directory, "unsafe-input.json");
            Assert.Equal(0, unsafePath == "hardlink" ? CreateHardLink(planPath, inputPath) : CreateFifo(inputPath, 0x180));
            planPath = inputPath;
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(directory, "intermediate"));
            imagePath = Path.Combine(directory, "intermediate", "..", "before-image.json");
        }
        var result = await RunCliAsync(["--connection-env", "CUTLIST_TASK9_FIXTURE_CONNECTION", "--expected-server", target.Server,
            "--expected-database", target.Database, "--apply", planPath, "--before-image", imagePath], true);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(before, await SnapshotAsync());
        Assert.Equal(JsonSerializer.Serialize(plan, Json), await File.ReadAllTextAsync(Path.Combine(directory, "reviewed.json")));
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int CreateHardLink(string existingPath, string newPath);
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int CreateFifo(string path, uint mode);

    private sealed record CliResult(string[] Arguments, int ExitCode, string Stdout, string Stderr);
    private async Task<CliResult> RunCliAsync(string[] arguments, bool secretPresent)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CutList.Web.Tests", "CutList.Web.Tests.csproj"))) root = root.Parent;
        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(root.FullName, "tools", "CutList.MaterialRepair", "bin", configuration, "net10.0", "CutList.MaterialRepair.dll");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = root.FullName };
        start.ArgumentList.Add(dll);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment.Remove("CUTLIST_TASK9_FIXTURE_CONNECTION");
        if (secretPresent) start.Environment["CUTLIST_TASK9_FIXTURE_CONNECTION"] = fixture.ConnectionString;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        var result = new CliResult(arguments, process.ExitCode, await stdout, await stderr);
        Assert.DoesNotContain(fixture.ConnectionString, result.Stdout + result.Stderr);
        Assert.DoesNotContain(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.ConnectionString).Password, result.Stdout + result.Stderr);
        return result;
    }

    private async Task<TestSeed> SeedAsync()
    {
        var seed = await fixture.ResetAsync();
        await using var context = await fixture.CreateContextAsync();
        var tube = await context.Materials.FindAsync(seed.RoundTubeMaterialId);
        tube!.Size = "1-1/2\" OD x 1/16\" wall";
        tube.UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.MaterialDimensions.Add(new RoundTubeDimensions { MaterialId = tube.Id, OuterDiameter = 1.5m, Wall = .083m });
        var channel = new Material { Shape = MaterialShape.Channel, Size = "3\" x 1-3/8\" x 1/8\"", Grade = "A36", Type = MaterialType.Steel };
        var custom = new Material { Shape = MaterialShape.SquareTube, Size = "Authored tube name", Grade = "A500", Type = MaterialType.Steel };
        context.Materials.AddRange(channel, custom, new Material { Shape = MaterialShape.RoundTube, Size = "Missing dimensions tube" });
        await context.SaveChangesAsync();
        context.MaterialDimensions.AddRange(new ChannelDimensions { MaterialId = channel.Id, Height = 3m, Flange = 1.41m, Web = .17m },
            new SquareTubeDimensions { MaterialId = custom.Id, Size = 2m, Wall = .083m });
        await context.SaveChangesAsync();
        return seed;
    }

    private async Task<RepairTarget> TargetAsync()
    {
        await using var context = await fixture.CreateContextAsync();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), DB_NAME(), service_broker_guid FROM sys.databases WHERE name = DB_NAME()";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new RepairTarget(reader.GetString(0), reader.GetString(1), reader.GetGuid(2));
    }

    private async Task<string> DimensionsAsync()
    {
        await using var context = await fixture.CreateContextAsync();
        var dimensions = await context.MaterialDimensions.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return JsonSerializer.Serialize(dimensions.Select(x => new { x.Id, x.MaterialId, Kind = x.GetType().Name,
            Values = x.GetType().GetProperties().Where(p => p.DeclaringType == x.GetType()).OrderBy(p => p.Name)
                .ToDictionary(p => p.Name, p => p.GetValue(x)?.ToString()) }));
    }
    private async Task<string> SnapshotAsync() => JsonSerializer.Serialize(new SnapshotEnvelope(await JobSnapshot.CaptureAsync(fixture), await DimensionsAsync()));
    private sealed record SnapshotEnvelope(JobSnapshot Catalog, string Dimensions);
}
