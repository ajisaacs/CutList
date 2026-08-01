using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialDimensions> MaterialDimensions => Set<MaterialDimensions>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<CuttingTool> CuttingTools => Set<CuttingTool>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobPart> JobParts => Set<JobPart>();
    public DbSet<JobStock> JobStocks => Set<JobStock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Material
        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Shape)
                .HasMaxLength(50)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),  // Enum to string (uses enum name)
                    v => Enum.Parse<MaterialShape>(v)); // String to enum
            entity.Property(e => e.Size).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<MaterialType>(v));
            entity.Property(e => e.Grade).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // MaterialDimensions - TPC inheritance (each shape gets its own table, no base table)
        modelBuilder.Entity<MaterialDimensions>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.UseTpcMappingStrategy();

            // 1:1 relationship with Material
            entity.HasOne(e => e.Material)
                .WithOne(m => m.Dimensions)
                .HasForeignKey<MaterialDimensions>(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure each dimension type's properties
        modelBuilder.Entity<RoundBarDimensions>(entity =>
        {
            entity.ToTable("DimRoundBar");
            entity.Property(e => e.Diameter).HasPrecision(10, 4);
            entity.HasIndex(e => e.Diameter);
        });

        modelBuilder.Entity<RoundTubeDimensions>(entity =>
        {
            entity.ToTable("DimRoundTube");
            entity.Property(e => e.OuterDiameter).HasPrecision(10, 4);
            entity.Property(e => e.Wall).HasPrecision(10, 4);
            entity.HasIndex(e => e.OuterDiameter);
        });

        modelBuilder.Entity<FlatBarDimensions>(entity =>
        {
            entity.ToTable("DimFlatBar");
            entity.Property(e => e.Width).HasPrecision(10, 4);
            entity.Property(e => e.Thickness).HasPrecision(10, 4);
            entity.HasIndex(e => e.Width);
        });

        modelBuilder.Entity<SquareBarDimensions>(entity =>
        {
            entity.ToTable("DimSquareBar");
            entity.Property(e => e.Size).HasPrecision(10, 4);
            entity.HasIndex(e => e.Size);
        });

        modelBuilder.Entity<SquareTubeDimensions>(entity =>
        {
            entity.ToTable("DimSquareTube");
            entity.Property(e => e.Size).HasPrecision(10, 4);
            entity.Property(e => e.Wall).HasPrecision(10, 4);
            entity.HasIndex(e => e.Size);
        });

        modelBuilder.Entity<RectangularTubeDimensions>(entity =>
        {
            entity.ToTable("DimRectangularTube");
            entity.Property(e => e.Width).HasPrecision(10, 4);
            entity.Property(e => e.Height).HasPrecision(10, 4);
            entity.Property(e => e.Wall).HasPrecision(10, 4);
            entity.HasIndex(e => e.Width);
        });

        modelBuilder.Entity<AngleDimensions>(entity =>
        {
            entity.ToTable("DimAngle");
            entity.Property(e => e.Leg1).HasPrecision(10, 4);
            entity.Property(e => e.Leg2).HasPrecision(10, 4);
            entity.Property(e => e.Thickness).HasPrecision(10, 4);
            entity.HasIndex(e => e.Leg1);
        });

        modelBuilder.Entity<ChannelDimensions>(entity =>
        {
            entity.ToTable("DimChannel");
            entity.Property(e => e.Height).HasPrecision(10, 4);
            entity.Property(e => e.Flange).HasPrecision(10, 4);
            entity.Property(e => e.Web).HasPrecision(10, 4);
            entity.HasIndex(e => e.Height);
        });

        modelBuilder.Entity<IBeamDimensions>(entity =>
        {
            entity.ToTable("DimIBeam");
            entity.Property(e => e.Height).HasPrecision(10, 4);
            entity.Property(e => e.WeightPerFoot).HasPrecision(10, 4);
            entity.HasIndex(e => e.Height);
        });

        modelBuilder.Entity<PipeDimensions>(entity =>
        {
            entity.ToTable("DimPipe");
            entity.Property(e => e.NominalSize).HasPrecision(10, 4);
            entity.Property(e => e.Wall).HasPrecision(10, 4);
            entity.Property(e => e.Schedule).HasMaxLength(20);
            entity.HasIndex(e => e.NominalSize);
        });

        // StockItem
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Material)
                .WithMany(m => m.StockItems)
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MaterialId, e.LengthInches }).IsUnique();
        });

        // StockTransaction
        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.StockItem)
                .WithMany(s => s.Transactions)
                .HasForeignKey(e => e.StockItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CuttingTool
        modelBuilder.Entity<CuttingTool>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.KerfInches).HasPrecision(6, 4);
        });

        // Job
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Customer).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.OptimizationResultJson).HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.JobNumber).IsUnique();

            entity.HasOne(e => e.CuttingTool)
                .WithMany(t => t.Jobs)
                .HasForeignKey(e => e.CuttingToolId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // JobPart
        modelBuilder.Entity<JobPart>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);

            entity.HasOne(e => e.Job)
                .WithMany(p => p.Parts)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Material)
                .WithMany(m => m.JobParts)
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // JobStock
        modelBuilder.Entity<JobStock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);

            entity.HasOne(e => e.Job)
                .WithMany(j => j.Stock)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StockItem)
                .WithMany()
                .HasForeignKey(e => e.StockItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Seed default cutting tools
        modelBuilder.Entity<CuttingTool>().HasData(
            new CuttingTool { Id = 1, Name = "Bandsaw", KerfInches = 0.0625m, IsDefault = true, IsActive = true },
            new CuttingTool { Id = 2, Name = "Chop Saw", KerfInches = 0.125m, IsDefault = false, IsActive = true },
            new CuttingTool { Id = 3, Name = "Cold Cut Saw", KerfInches = 0.0625m, IsDefault = false, IsActive = true },
            new CuttingTool { Id = 4, Name = "Hacksaw", KerfInches = 0.0625m, IsDefault = false, IsActive = true }
        );
    }
}
