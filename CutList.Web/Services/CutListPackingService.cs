using CutList.Core;
using CutList.Core.Nesting;
using CutList.Web.Data.Entities;

namespace CutList.Web.Services;

public class CutListPackingService
{
    public PackResult Pack(IEnumerable<ProjectPart> parts, IEnumerable<ProjectStockBin> stockBins, decimal kerfInches)
    {
        var engine = new MultiBinEngine();
        engine.Spacing = (double)kerfInches;
        engine.Strategy = PackingStrategy.AdvancedFit;

        // Convert stock bins to MultiBin
        var multiBins = stockBins
            .Where(b => b.LengthInches > 0)
            .Select(b => new MultiBin((double)b.LengthInches, b.Quantity, b.Priority))
            .ToList();

        engine.SetBins(multiBins);

        // Convert parts to BinItem (expand quantity)
        var items = parts
            .SelectMany(p => Enumerable.Range(0, p.Quantity)
                .Select(_ => new BinItem(p.Name, (double)p.LengthInches)))
            .ToList();

        return engine.Pack(items);
    }

    public PackingSummary GetSummary(PackResult result)
    {
        var summary = new PackingSummary();

        foreach (var bin in result.Bins)
        {
            summary.TotalBins++;
            summary.TotalMaterial += bin.Length;
            summary.TotalUsed += bin.UsedLength;
            summary.TotalWaste += bin.RemainingLength;
            summary.TotalPieces += bin.Items.Count;
        }

        summary.ItemsNotPlaced = result.ItemsNotUsed.Count;

        if (summary.TotalMaterial > 0)
        {
            summary.Efficiency = summary.TotalUsed / summary.TotalMaterial * 100;
        }

        return summary;
    }
}

public class PackingSummary
{
    public int TotalBins { get; set; }
    public int TotalPieces { get; set; }
    public double TotalMaterial { get; set; }
    public double TotalUsed { get; set; }
    public double TotalWaste { get; set; }
    public double Efficiency { get; set; }
    public int ItemsNotPlaced { get; set; }
}
