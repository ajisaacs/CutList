using CutList.Core;
using CutList.Core.Nesting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class CutListPackingService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly IPackingEngineCatalog _engines;

    public CutListPackingService(IDbContextFactory<ApplicationDbContext> factory, IPackingEngineCatalog engines)
    {
        _factory = factory;
        _engines = engines;
    }

    public async Task<MultiMaterialPackResult> PackAsync(IEnumerable<JobPart> parts, decimal kerfInches)
    {
        return await PackAsync(parts, kerfInches, null);
    }

    public async Task<MultiMaterialPackResult> PackAsync(IEnumerable<JobPart> parts, decimal kerfInches, IEnumerable<JobStock>? jobStock)
    {
        await using var context = _factory.CreateDbContext();
        var result = new MultiMaterialPackResult();

        // Group parts by material
        var partsByMaterial = parts.GroupBy(p => p.MaterialId);

        // Group job stock by material for quick lookup
        var jobStockByMaterial = jobStock?.GroupBy(s => s.MaterialId)
            .ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<int, List<JobStock>>();

        foreach (var group in partsByMaterial)
        {
            var materialId = group.Key;
            var materialParts = group.ToList();

            // Get the material
            var material = await context.Materials
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material == null) continue;

            // Build stock bins
            var stockBins = new List<StockBinSource>();

            // Use job-specific stock configuration. Jobs must have stock explicitly configured -
            // there is no fallback to "whatever's in inventory."
            if (jobStockByMaterial.TryGetValue(materialId, out var materialJobStock))
            {
                foreach (var stock in materialJobStock.OrderBy(s => s.Priority))
                {
                    stockBins.Add(new StockBinSource
                    {
                        LengthInches = stock.LengthInches,
                        Quantity = stock.Quantity,
                        Priority = stock.Priority,
                        IsInStock = !stock.IsCustomLength && stock.StockItemId.HasValue
                    });
                }
            }

            if (stockBins.Count == 0)
            {
                // No stock available for this material - mark all parts as not placed
                var materialResult = new MaterialPackResult
                {
                    Material = material,
                    PackResult = new PackResult(),
                    InStockBins = new List<Bin>(),
                    ToBePurchasedBins = new List<Bin>()
                };

                // Add all parts as not used
                foreach (var part in materialParts)
                {
                    for (int i = 0; i < part.Quantity; i++)
                    {
                        materialResult.PackResult.AddItemNotUsed(new BinItem(part.Name, (double)part.LengthInches));
                    }
                }

                result.MaterialResults.Add(materialResult);
                continue;
            }

            // Run the packing algorithm
            var engine = new MultiBinEngine(_engines.Create(null));
            engine.Spacing = (double)kerfInches;

            var multiBins = stockBins
                .Select(b => new MultiBin((double)b.LengthInches, b.Quantity, b.Priority))
                .ToList();

            engine.SetBins(multiBins);

            var items = materialParts
                .SelectMany(p => Enumerable.Range(0, p.Quantity)
                    .Select(_ => new BinItem(p.Name, (double)p.LengthInches)))
                .ToList();

            var packResult = engine.Pack(items);

            // Separate bins into in-stock and to-be-purchased
            var inStockBins = new List<Bin>();
            var toBePurchasedBins = new List<Bin>();

            // Catalog-sourced stock rows with an unlimited (-1) quantity are always in-stock,
            // regardless of how many bins of that length get packed.
            var unlimitedInStockLengths = stockBins
                .Where(s => s.IsInStock && s.Quantity == -1)
                .Select(s => s.LengthInches)
                .ToHashSet();

            // Track remaining in-stock quantities from the finite-quantity catalog stock bins we configured
            var remainingStock = stockBins
                .Where(s => s.IsInStock && s.Quantity > 0)
                .GroupBy(s => s.LengthInches)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));

            foreach (var bin in packResult.Bins)
            {
                var binLength = (decimal)bin.Length;

                if (unlimitedInStockLengths.Contains(binLength))
                {
                    // Unlimited catalog-sourced stock - always in-stock
                    inStockBins.Add(bin);
                }
                else if (remainingStock.TryGetValue(binLength, out var remaining) && remaining > 0)
                {
                    // Check if this can come from in-stock
                    inStockBins.Add(bin);
                    remainingStock[binLength] = remaining - 1;
                }
                else
                {
                    toBePurchasedBins.Add(bin);
                }
            }

            result.MaterialResults.Add(new MaterialPackResult
            {
                Material = material,
                PackResult = packResult,
                InStockBins = inStockBins,
                ToBePurchasedBins = toBePurchasedBins
            });
        }

        return result;
    }

    public async Task<MultiMaterialPackResult?> LoadSavedResultAsync(string json)
    {
        var saved = System.Text.Json.JsonSerializer.Deserialize<SavedOptimizationResult>(json);
        if (saved == null) return null;
        await using var context = _factory.CreateDbContext();
        return await saved.ToPackResultAsync(context);
    }

    public string SerializeResult(MultiMaterialPackResult result)
    {
        var saved = SavedOptimizationResult.FromPackResult(result);
        return System.Text.Json.JsonSerializer.Serialize(saved);
    }

    public MultiMaterialPackingSummary GetSummary(MultiMaterialPackResult result)
    {
        var summary = new MultiMaterialPackingSummary();

        foreach (var materialResult in result.MaterialResults)
        {
            var materialSummary = new MaterialPackingSummary
            {
                Material = materialResult.Material
            };

            foreach (var bin in materialResult.InStockBins)
            {
                materialSummary.InStockBins++;
                materialSummary.TotalMaterial += bin.Length;
                materialSummary.TotalUsed += bin.UsedLength;
                materialSummary.TotalWaste += bin.RemainingLength;
                materialSummary.TotalPieces += bin.Items.Count;
            }

            foreach (var bin in materialResult.ToBePurchasedBins)
            {
                materialSummary.ToBePurchasedBins++;
                materialSummary.TotalMaterial += bin.Length;
                materialSummary.TotalUsed += bin.UsedLength;
                materialSummary.TotalWaste += bin.RemainingLength;
                materialSummary.TotalPieces += bin.Items.Count;
            }

            materialSummary.ItemsNotPlaced = materialResult.PackResult.ItemsNotUsed.Count;

            if (materialSummary.TotalMaterial > 0)
            {
                materialSummary.Efficiency = materialSummary.TotalUsed / materialSummary.TotalMaterial * 100;
            }

            summary.MaterialSummaries.Add(materialSummary);

            // Aggregate totals
            summary.TotalInStockBins += materialSummary.InStockBins;
            summary.TotalToBePurchasedBins += materialSummary.ToBePurchasedBins;
            summary.TotalPieces += materialSummary.TotalPieces;
            summary.TotalMaterial += materialSummary.TotalMaterial;
            summary.TotalUsed += materialSummary.TotalUsed;
            summary.TotalWaste += materialSummary.TotalWaste;
            summary.TotalItemsNotPlaced += materialSummary.ItemsNotPlaced;
        }

        if (summary.TotalMaterial > 0)
        {
            summary.Efficiency = summary.TotalUsed / summary.TotalMaterial * 100;
        }

        return summary;
    }
}

public class StockBinSource
{
    public decimal LengthInches { get; set; }
    public int Quantity { get; set; }
    public int Priority { get; set; }
    public bool IsInStock { get; set; }
}

public class MultiMaterialPackResult
{
    public List<MaterialPackResult> MaterialResults { get; set; } = new();
}

public class MaterialPackResult
{
    public Material Material { get; set; } = null!;
    public PackResult PackResult { get; set; } = null!;
    public List<Bin> InStockBins { get; set; } = new();
    public List<Bin> ToBePurchasedBins { get; set; } = new();
}

public class MultiMaterialPackingSummary
{
    public List<MaterialPackingSummary> MaterialSummaries { get; set; } = new();
    public int TotalInStockBins { get; set; }
    public int TotalToBePurchasedBins { get; set; }
    public int TotalPieces { get; set; }
    public double TotalMaterial { get; set; }
    public double TotalUsed { get; set; }
    public double TotalWaste { get; set; }
    public double Efficiency { get; set; }
    public int TotalItemsNotPlaced { get; set; }
}

public class MaterialPackingSummary
{
    public Material Material { get; set; } = null!;
    public int InStockBins { get; set; }
    public int ToBePurchasedBins { get; set; }
    public int TotalPieces { get; set; }
    public double TotalMaterial { get; set; }
    public double TotalUsed { get; set; }
    public double TotalWaste { get; set; }
    public double Efficiency { get; set; }
    public int ItemsNotPlaced { get; set; }
}

// --- Serialization DTOs for persisting optimization results ---

public class SavedBinItem
{
    public string Name { get; set; } = string.Empty;
    public double Length { get; set; }
}

public class SavedBin
{
    public double Length { get; set; }
    public double Spacing { get; set; }
    public List<SavedBinItem> Items { get; set; } = new();
}

public class SavedMaterialResult
{
    public int MaterialId { get; set; }
    public string MaterialDisplayName { get; set; } = string.Empty;
    public List<SavedBin> InStockBins { get; set; } = new();
    public List<SavedBin> ToBePurchasedBins { get; set; } = new();
    public List<SavedBinItem> ItemsNotPlaced { get; set; } = new();
}

public class SavedOptimizationResult
{
    public DateTime OptimizedAt { get; set; }
    public List<SavedMaterialResult> MaterialResults { get; set; } = new();

    public static SavedOptimizationResult FromPackResult(MultiMaterialPackResult result)
    {
        var saved = new SavedOptimizationResult
        {
            OptimizedAt = DateTime.UtcNow
        };

        foreach (var mr in result.MaterialResults)
        {
            var savedMr = new SavedMaterialResult
            {
                MaterialId = mr.Material.Id,
                MaterialDisplayName = mr.Material.DisplayName
            };

            savedMr.InStockBins = mr.InStockBins.Select(ToBinDto).ToList();
            savedMr.ToBePurchasedBins = mr.ToBePurchasedBins.Select(ToBinDto).ToList();
            savedMr.ItemsNotPlaced = mr.PackResult.ItemsNotUsed
                .Select(i => new SavedBinItem { Name = i.Name, Length = i.Length })
                .ToList();

            saved.MaterialResults.Add(savedMr);
        }

        return saved;
    }

    public async Task<MultiMaterialPackResult> ToPackResultAsync(ApplicationDbContext context)
    {
        var result = new MultiMaterialPackResult();

        foreach (var savedMr in MaterialResults)
        {
            var material = await context.Materials.FindAsync(savedMr.MaterialId);
            if (material == null) continue;

            var packResult = new PackResult();
            var inStockBins = savedMr.InStockBins.Select(FromBinDto).ToList();
            var toBePurchasedBins = savedMr.ToBePurchasedBins.Select(FromBinDto).ToList();

            // Add all bins to PackResult so summary calculations work
            foreach (var bin in inStockBins) packResult.AddBin(bin);
            foreach (var bin in toBePurchasedBins) packResult.AddBin(bin);
            foreach (var item in savedMr.ItemsNotPlaced)
                packResult.AddItemNotUsed(new BinItem(item.Name, item.Length));

            result.MaterialResults.Add(new MaterialPackResult
            {
                Material = material,
                PackResult = packResult,
                InStockBins = inStockBins,
                ToBePurchasedBins = toBePurchasedBins
            });
        }

        return result;
    }

    private static SavedBin ToBinDto(Bin bin)
    {
        return new SavedBin
        {
            Length = bin.Length,
            Spacing = bin.Spacing,
            Items = bin.Items.Select(i => new SavedBinItem { Name = i.Name, Length = i.Length }).ToList()
        };
    }

    private static Bin FromBinDto(SavedBin dto)
    {
        var bin = new Bin(dto.Length) { Spacing = dto.Spacing };
        foreach (var item in dto.Items)
            bin.AddItem(new BinItem(item.Name, item.Length));
        return bin;
    }
}
