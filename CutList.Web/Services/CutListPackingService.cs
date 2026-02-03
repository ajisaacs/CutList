using CutList.Core;
using CutList.Core.Nesting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class CutListPackingService
{
    private readonly ApplicationDbContext _context;

    public CutListPackingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MultiMaterialPackResult> PackAsync(IEnumerable<ProjectPart> parts, decimal kerfInches)
    {
        var result = new MultiMaterialPackResult();

        // Group parts by material
        var partsByMaterial = parts.GroupBy(p => p.MaterialId);

        foreach (var group in partsByMaterial)
        {
            var materialId = group.Key;
            var materialParts = group.ToList();

            // Get the material
            var material = await _context.Materials
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material == null) continue;

            // Get in-stock lengths for this material
            var inStockLengths = await _context.MaterialStockLengths
                .Where(s => s.MaterialId == materialId && s.IsActive && s.Quantity > 0)
                .ToListAsync();

            // Get stock item lengths for this material (for purchase)
            var stockItemLengths = await _context.StockItems
                .Where(s => s.MaterialId == materialId && s.IsActive)
                .Select(s => s.LengthInches)
                .Distinct()
                .ToListAsync();

            // Build stock bins: in-stock first (priority 1), then supplier stock (priority 2)
            var stockBins = new List<StockBinSource>();

            // In-stock bins with finite quantity
            foreach (var stock in inStockLengths)
            {
                stockBins.Add(new StockBinSource
                {
                    LengthInches = stock.LengthInches,
                    Quantity = stock.Quantity,
                    Priority = 1,
                    IsInStock = true
                });
            }

            // Stock item bins with unlimited quantity
            foreach (var length in stockItemLengths)
            {
                // Only add if not already covered by in-stock
                if (!stockBins.Any(b => b.LengthInches == length && b.IsInStock))
                {
                    stockBins.Add(new StockBinSource
                    {
                        LengthInches = length,
                        Quantity = -1, // unlimited
                        Priority = 2,
                        IsInStock = false
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
            var engine = new MultiBinEngine();
            engine.Spacing = (double)kerfInches;
            engine.Strategy = PackingStrategy.AdvancedFit;

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

            // Track remaining in-stock quantities
            var remainingStock = inStockLengths.ToDictionary(
                s => s.LengthInches,
                s => s.Quantity);

            foreach (var bin in packResult.Bins)
            {
                var binLength = (decimal)bin.Length;

                // Check if this can come from in-stock
                if (remainingStock.TryGetValue(binLength, out var remaining) && remaining > 0)
                {
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
