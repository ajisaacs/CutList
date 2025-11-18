using CutList.Forms;
using Newtonsoft.Json;
using System;
using System.IO;

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
        /// <exception cref="IOException">Thrown when file cannot be saved</exception>
        public void Save(Document document, string filePath)
        {
            var json = JsonConvert.SerializeObject(document, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Loads a document from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path to load from</param>
        /// <returns>The loaded document</returns>
        /// <exception cref="IOException">Thrown when file cannot be read</exception>
        /// <exception cref="JsonException">Thrown when file contains invalid JSON</exception>
        public Document Load(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var document = JsonConvert.DeserializeObject<Document>(json);
            return document;
        }

        /// <summary>
        /// Validates that a document has the minimum required data.
        /// </summary>
        /// <param name="document">The document to validate</param>
        /// <param name="validationMessage">Output parameter containing validation error message</param>
        /// <returns>True if document is valid, false otherwise</returns>
        public bool Validate(Document document, out string validationMessage)
        {
            if (document.PartsToNest == null || document.PartsToNest.Count == 0)
            {
                validationMessage = "No parts to nest.";
                return false;
            }

            if (document.StockBins == null || document.StockBins.Count == 0)
            {
                validationMessage = "No stock bins available.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }
    }
}
