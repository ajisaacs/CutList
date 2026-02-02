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
    public DbSet<MaterialStockLength> MaterialStockLengths => Set<MaterialStockLength>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierStock> SupplierStocks => Set<SupplierStock>();
    public DbSet<CuttingTool> CuttingTools => Set<CuttingTool>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectPart> ProjectParts => Set<ProjectPart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Material
        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Shape).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Size).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // MaterialStockLength
        modelBuilder.Entity<MaterialStockLength>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);
            entity.Property(e => e.Notes).HasMaxLength(255);

            entity.HasOne(e => e.Material)
                .WithMany(m => m.StockLengths)
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MaterialId, e.LengthInches }).IsUnique();
        });

        // Supplier
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ContactInfo).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // SupplierStock
        modelBuilder.Entity<SupplierStock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.Notes).HasMaxLength(255);

            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.Stocks)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Material)
                .WithMany(m => m.SupplierStocks)
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SupplierId, e.MaterialId, e.LengthInches }).IsUnique();
        });

        // CuttingTool
        modelBuilder.Entity<CuttingTool>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.KerfInches).HasPrecision(6, 4);
        });

        // Project
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Customer).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.CuttingTool)
                .WithMany(t => t.Projects)
                .HasForeignKey(e => e.CuttingToolId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ProjectPart
        modelBuilder.Entity<ProjectPart>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Parts)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Material)
                .WithMany(m => m.ProjectParts)
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
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
