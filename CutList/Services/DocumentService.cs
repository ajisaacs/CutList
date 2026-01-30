using CutList.Common;
using CutList.Models;
using Newtonsoft.Json;

namespace CutList.Services
{
    /// <summary>
    /// Service class that handles document persistence operations.
    /// Separates file I/O logic from UI concerns.
    /// </summary>
    public class DocumentService
    {
        /// <summary>
        /// Saves a document to the specified file path.
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="filePath">The file path to save to</param>
        /// <returns>Result indicating success or failure with error message</returns>
        public Result Save(Document document, string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(document, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to save file: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a document from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path to load from</param>
        /// <returns>Result containing the loaded document or error message</returns>
        public Result<Document> Load(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var document = JsonConvert.DeserializeObject<Document>(json);
                return Result<Document>.Success(document);
            }
            catch (Exception ex)
            {
                return Result<Document>.Failure($"Failed to load file: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that a document has the minimum required data.
        /// </summary>
        /// <param name="document">The document to validate</param>
        /// <returns>Result indicating success or failure with validation error message</returns>
        public Result Validate(Document document)
        {
            if (document.PartsToNest == null || document.PartsToNest.Count == 0)
            {
                return Result.Failure("No parts to nest.");
            }

            if (document.StockBins == null || document.StockBins.Count == 0)
            {
                return Result.Failure("No stock bins available.");
            }

            return Result.Success();
        }
    }
}
