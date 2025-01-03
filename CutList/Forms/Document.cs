using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using CutList.Models;
using Newtonsoft.Json;

namespace CutList.Forms
{
    public class Document
    {
        public Document()
        {
            PartsToNest = new List<PartInputItem>();
            StockBins = new List<BinInputItem>();
        }

        [JsonIgnore]
        public string LastFilePath { get; private set; }

        public List<PartInputItem> PartsToNest { get; set; }

        public List<BinInputItem> StockBins { get; set; }

        public Tool Tool { get; set; }

        public void Save(string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filePath, json);
                LastFilePath = filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static Document Load(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var document = JsonConvert.DeserializeObject<Document>(json);
                document.LastFilePath = filePath;
                return document;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        public bool Validate(out string validationMessage)
        {
            if (PartsToNest == null || !PartsToNest.Any())
            {
                validationMessage = "No parts to nest.";
                return false;
            }

            if (StockBins == null || !StockBins.Any())
            {
                validationMessage = "No stock bins available.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }
    }
}