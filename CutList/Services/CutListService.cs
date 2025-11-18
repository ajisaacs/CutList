using CutList.Common;
using CutList.Models;
using SawCut;
using SawCut.Nesting;
using System;
using System.Collections.Generic;

namespace CutList.Services
{
    /// <summary>
    /// Service class that handles the core business logic for cut list optimization.
    /// Separates business logic from UI concerns.
    /// </summary>
    public class CutListService
    {
        /// <summary>
        /// Runs the bin packing algorithm to optimize cut lists.
        /// </summary>
        /// <param name="parts">The parts to be nested</param>
        /// <param name="stockBins">The available stock bins</param>
        /// <param name="cuttingTool">The cutting tool to use (determines kerf/spacing)</param>
        /// <returns>Result containing the packing result with optimized bins and unused items, or error message</returns>
        public Result<SawCut.Nesting.Result> Pack(List<PartInputItem> parts, List<BinInputItem> stockBins, Tool cuttingTool)
        {
            try
            {
                var multiBins = ConvertToMultiBins(stockBins);
                var binItems = ConvertToBinItems(parts);

                var engine = new MultiBinEngine
                {
                    Spacing = cuttingTool.Kerf,
                    Bins = multiBins
                };

                var packResult = engine.Pack(binItems);
                return Result<SawCut.Nesting.Result>.Success(packResult);
            }
            catch (Exception ex)
            {
                return Result<SawCut.Nesting.Result>.Failure($"Packing failed: {ex.Message}");
            }
        }

        private List<MultiBin> ConvertToMultiBins(List<BinInputItem> stockBins)
        {
            var multiBins = new List<MultiBin>();

            foreach (var item in stockBins)
            {
                multiBins.Add(new MultiBin
                {
                    Length = item.Length.Value,
                    Quantity = item.Quantity,
                    Priority = item.Priority
                });
            }

            return multiBins;
        }

        private List<BinItem> ConvertToBinItems(List<PartInputItem> parts)
        {
            var binItems = new List<BinItem>();

            foreach (var part in parts)
            {
                if (part.Length == null || part.Length == 0)
                    continue;

                for (int i = 0; i < part.Quantity; i++)
                {
                    binItems.Add(new BinItem
                    {
                        Name = part.Name,
                        Length = part.Length.Value
                    });
                }
            }

            return binItems;
        }
    }
}
