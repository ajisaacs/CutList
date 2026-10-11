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
    public DbSet<CuttingTool> CuttingTools => Set<CuttingTool>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobPart> JobParts => Set<JobPart>();
    public DbSet<JobPartSetInstance> JobPartSetInstances => Set<JobPartSetInstance>();
    public DbSet<JobStock> JobStocks => Set<JobStock>();

    public DbSet<PartSet> PartSets => Set<PartSet>();
    public DbSet<PartSetMember> PartSetMembers => Set<PartSetMember>();

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

        // CuttingTool
        modelBuilder.Entity<CuttingTool>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.KerfInches).HasPrecision(6, 4);
        });

        modelBuilder.Entity<PartSet>(entity =>
        {
            entity.Property(s => s.DrawingNumber).HasMaxLength(100).IsRequired();
            entity.Property(s => s.Revision).HasMaxLength(50);
            entity.Property(s => s.DrawingKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
            entity.Property(s => s.RevisionKey).HasMaxLength(50).UseCollation("Latin1_General_100_BIN2").IsRequired();
            entity.Property(s => s.DisplayName).HasMaxLength(100).IsRequired();
            entity.HasIndex(s => new { s.DrawingKey, s.RevisionKey }).IsUnique();
        });
        modelBuilder.Entity<PartSetMember>(entity =>
        {
            entity.ToTable(t => { t.HasCheckConstraint("CK_PartSetMember_Length", "[LengthInches] > 0"); t.HasCheckConstraint("CK_PartSetMember_Quantity", "[Quantity] > 0"); });
            entity.Property(m => m.Name).HasMaxLength(100).IsRequired();
            entity.Property(m => m.SourceItem).HasMaxLength(100);
            entity.Property(m => m.SourceFile).HasMaxLength(255);
            entity.Property(m => m.LengthInches).HasPrecision(10, 4);
            entity.HasOne(m => m.PartSet).WithMany(s => s.Members).HasForeignKey(m => m.PartSetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(m => m.Material).WithMany().HasForeignKey(m => m.MaterialId).OnDelete(DeleteBehavior.NoAction);
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

            // Lock enforcement: job UPDATE/DELETE statements include the LockedAt value that was read,
            // so a concurrent lock/unlock makes the save fail instead of overwriting the new state.
            entity.Property(e => e.LockedAt).IsConcurrencyToken();

            entity.HasIndex(e => e.JobNumber).IsUnique();

            entity.HasOne(e => e.CuttingTool)
                .WithMany(t => t.Jobs)
                .HasForeignKey(e => e.CuttingToolId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JobPartSetInstance>(entity =>
        {
            entity.HasAlternateKey(e => new { e.Id, e.JobId });
            entity.Property(e => e.DrawingNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Revision).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
            entity.ToTable(t => t.HasCheckConstraint("CK_JobPartSetInstance_Quantity", "[Quantity] > 0"));
            entity.HasOne(e => e.Job).WithMany(j => j.PartSetInstances)
                .HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PartSet).WithMany()
                .HasForeignKey(e => e.PartSetId).OnDelete(DeleteBehavior.NoAction);
        });

        // JobPart
        modelBuilder.Entity<JobPart>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);

            // The composite FK also prevents linking a part to another job's instance.
            entity.HasOne(e => e.PartSetInstance).WithMany(i => i.Parts)
                .HasForeignKey(e => new { e.PartSetInstanceId, e.JobId })
                .HasPrincipalKey(i => new { i.Id, i.JobId }).OnDelete(DeleteBehavior.NoAction);

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
