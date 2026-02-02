using CutList.Core;
using CutList.Core.Formatting;
using CutList.Core.Nesting;
using CutList.Web.Data.Entities;

namespace CutList.Web.Services;

public class ReportService
{
    public string FormatLength(double inches)
    {
        return ArchUnits.FormatFromInches(inches);
    }

    public List<ItemGroup> GroupItems(IReadOnlyList<BinItem> items)
    {
        return items
            .GroupBy(i => new { i.Name, i.Length })
            .Select(g => new ItemGroup
            {
                Name = g.Key.Name,
                Length = g.Key.Length,
                Count = g.Count()
            })
            .OrderByDescending(g => g.Length)
            .ToList();
    }
}

public class ItemGroup
{
    public string Name { get; set; } = string.Empty;
    public double Length { get; set; }
    public int Count { get; set; }
}
