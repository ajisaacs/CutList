using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CutList.Web.Data;
using CutList.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32.SafeHandles;

namespace CutList.MaterialRepair;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = Parse(args);
            var connection = Environment.GetEnvironmentVariable(options["--connection-env"]);
            if (string.IsNullOrWhiteSpace(connection)) throw new CliUsageException("Explicit connection environment variable is missing or empty.");
            // Deliberately no host, configuration provider, appsettings, logging, retry or migration.
            var factory = new ContextFactory(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
            var service = new MaterialSizeRepair(factory);
            var server = options["--expected-server"];
            var database = options["--expected-database"];
            if (options.TryGetValue("--apply", out var reviewedPath))
            {
                var imagePath = options["--before-image"];
                ValidateArtifactPath(imagePath, mustBeNew: true);
                var manifest = await ReadAsync<MaterialRepairManifest>(reviewedPath);
                var count = await service.ApplyAsync(manifest, server, database, image => WriteDurableAsync(imagePath, image));
                Console.WriteLine($"applied: rows={count}; before-image durable; rollback requires unchanged applied label/identity/dimensions/timestamp.");
            }
            else if (options.TryGetValue("--rollback", out var rollbackPath))
            {
                var manifest = await ReadAsync<MaterialRepairRollback>(rollbackPath);
                var count = await service.RollbackAsync(manifest, server, database);
                Console.WriteLine($"rolled-back: rows={count}; original Size and UpdatedAt restored.");
            }
            else
            {
                var path = options["--manifest"];
                ValidateArtifactPath(path, mustBeNew: true);
                var manifest = await service.DryRunAsync(server, database);
                await WriteDurableAsync(path, manifest);
                var counts = manifest.Entries.GroupBy(e => e.Status).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}");
                Console.WriteLine($"dry-run: total={manifest.Entries.Count}; {string.Join("; ", counts)}; database unchanged.");
            }
            return 0;
        }
        catch (CliUsageException error)
        {
            Console.Error.WriteLine("error: " + error.Message);
            return 2;
        }
        catch (MaterialRepairRejectedException error)
        {
            Console.Error.WriteLine("rejected: " + error.Message);
            return 3;
        }
        catch (Exception)
        {
            // SQL, IO and JSON errors can include user content or secrets. Never print exception
            // messages, stack traces, connection strings, env values, unrecognized args or paths.
            Console.Error.WriteLine("error: database or artifact operation failed; details suppressed to protect secrets. No automatic retry.");
            return 4;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var allowed = new HashSet<string>(["--connection-env", "--expected-server", "--expected-database", "--manifest", "--before-image", "--apply", "--rollback", "--dry-run"]);
        var modes = 0;
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!allowed.Contains(key) || options.ContainsKey(key)) throw new CliUsageException("Unknown or repeated option.");
            if (key is "--apply" or "--rollback" or "--dry-run") modes++;
            if (key == "--dry-run") { options.Add(key, "true"); continue; }
            if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--", StringComparison.Ordinal))
                throw new CliUsageException("Option value is missing.");
            options.Add(key, args[i]);
        }
        if (modes > 1) throw new CliUsageException("Choose exactly one mode (default is dry-run).");
        foreach (var key in new[] { "--connection-env", "--expected-server", "--expected-database" })
            if (!options.ContainsKey(key)) throw new CliUsageException("Connection env name and expected server/database are required.");
        if (!Regex.IsMatch(options["--connection-env"], @"\A[A-Za-z_][A-Za-z0-9_]*\z")) throw new CliUsageException("Invalid environment variable name.");
        var apply = options.ContainsKey("--apply");
        var rollback = options.ContainsKey("--rollback");
        if (apply && !options.ContainsKey("--before-image")) throw new CliUsageException("Apply requires a new absolute before-image artifact path.");
        if (!apply && options.ContainsKey("--before-image")) throw new CliUsageException("Before-image is only valid with apply.");
        if ((apply || rollback) && options.ContainsKey("--manifest")) throw new CliUsageException("Manifest output is only valid with dry-run.");
        if (!apply && !rollback && !options.ContainsKey("--manifest")) throw new CliUsageException("Dry-run requires a new absolute manifest artifact path.");
        return options;
    }

    private static async Task<T> ReadAsync<T>(string path)
    {
        ValidateArtifactPath(path, mustBeNew: false);
        using var directory = OpenArtifactDirectory(path);
        using var handle = OpenArtifact(directory, path, create: false);
        await using var file = new FileStream(handle, FileAccess.Read);
        return await JsonSerializer.DeserializeAsync<T>(file, Json) ?? throw new CliUsageException("Empty manifest.");
    }

    private static void ValidateArtifactPath(string path, bool mustBeNew)
    {
        // Fail closed on platforms without the descriptor-relative, no-symlink and directory
        // durability implementation below. Do not silently downgrade apply's recovery guarantee.
        if (!OperatingSystem.IsLinux()) throw new CliUsageException("Durable artifact handling currently requires Linux.");
        if (!Path.IsPathFullyQualified(path)) throw new CliUsageException("Artifact paths must be absolute.");
        var full = Path.GetFullPath(path);
        if (!string.Equals(full, path, StringComparison.Ordinal)) throw new CliUsageException("Artifact paths must be canonical (no dot segments or redundant separators).");
        var parent = new DirectoryInfo(Path.GetDirectoryName(full)!);
        if (!parent.Exists) throw new CliUsageException("Artifact parent directory must already exist.");
        for (var directory = parent; directory != null; directory = directory.Parent)
            if (directory.LinkTarget != null) throw new CliUsageException("Symlink artifact directories are unsafe.");
        var file = new FileInfo(full);
        if (file.LinkTarget != null) throw new CliUsageException("Symlink artifacts are unsafe.");
        if (mustBeNew && (file.Exists || Directory.Exists(full))) throw new CliUsageException("Refusing to overwrite an existing artifact.");
        if (!mustBeNew && !file.Exists) throw new CliUsageException("Input artifact does not exist.");
    }

    private static async Task WriteDurableAsync<T>(string path, T artifact)
    {
        ValidateArtifactPath(path, mustBeNew: true);
        using var directory = OpenArtifactDirectory(path, ensureDurablePath: true);
        using var handle = OpenArtifact(directory, path, create: true);
        await using (var file = new FileStream(handle, FileAccess.Write))
        {
            await JsonSerializer.SerializeAsync(file, artifact, Json);
            await file.FlushAsync();
            file.Flush(flushToDisk: true); // Must complete before service permits any UPDATE.
        }
        // Flush the SAME directory descriptor used to create the artifact, not a reopened path.
        // This anchors both creation and fsync against a concurrent parent-symlink replacement.
        if (FlushDirectory(directory) != 0) throw new IOException("Directory flush failed.");
    }

    private const int NoFollow = 0x20000;
    private const int CloseOnExec = 0x80000;
    private const int DirectoryFlags = 0x10000 | NoFollow | CloseOnExec;

    private static SafeFileHandle OpenArtifactDirectory(string path, bool ensureDurablePath = false)
    {
        var descriptor = OpenDirectory("/", DirectoryFlags);
        if (descriptor < 0) throw new IOException("Directory open failed.");
        var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        try
        {
            // Resolve each component relative to an already-open directory. A separate check
            // followed by FileStream(path) would leave a symlink-swap TOCTOU window.
            foreach (var component in Path.GetDirectoryName(path)!.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                // A freshly created artifact directory also needs its entry in its own parent
                // made durable. Flush each ancestor while its descriptor is still anchored.
                if (ensureDurablePath && FlushDirectory(handle) != 0) throw new IOException("Directory flush failed.");
                descriptor = OpenAt(handle, component, DirectoryFlags, 0);
                if (descriptor < 0) throw new IOException("Directory open failed.");
                handle.Dispose();
                handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
            }
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    private static SafeFileHandle OpenArtifact(SafeFileHandle directory, string path, bool create)
    {
        // O_EXCL prevents overwrite races; O_NOFOLLOW rejects last-component symlink races.
        // O_NONBLOCK prevents a substituted FIFO input from hanging before its type is checked.
        var flags = NoFollow | CloseOnExec | (create ? 0x1 | 0x40 | 0x80 : 0x800);
        var descriptor = OpenAt(directory, Path.GetFileName(path), flags, 0x180); // mode 0600
        if (descriptor < 0) throw new IOException("Artifact open failed.");
        var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        if (StatArtifact(handle, "", 0x1000, 0x7ff, out var stat) != 0 || (stat.Mode & 0xf000) != 0x8000 || stat.LinkCount != 1)
        {
            handle.Dispose();
            throw new CliUsageException("Artifacts must be regular files with exactly one hard link.");
        }
        return handle;
    }

    // Linux statx has a fixed, architecture-independent 256-byte ABI.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct ArtifactStat
    {
        [FieldOffset(16)] public uint LinkCount;
        [FieldOffset(28)] public ushort Mode;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenDirectory(string path, int flags);
    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAt(SafeFileHandle directory, string name, int flags, uint mode);
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int StatArtifact(SafeFileHandle file, string path, int flags, uint mask, out ArtifactStat stat);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int FlushDirectory(SafeFileHandle descriptor);

    private sealed class CliUsageException(string message) : Exception(message);
    private sealed class ContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
