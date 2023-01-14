using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace CutList.Forms
{
    public class Toolbox
    {
        public Toolbox()
        {
            Load();
        }

        public List<Tool> Tools { get; set; } 

        public string ToolsFilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data\\Tools.json");

        private void LoadDefaultTools()
        {
            Tools = new List<Tool>
            {
                new Tool { Name = "Shear", Kerf = 0.0 },
                new Tool { Name = "Saw", Kerf = 0.125 },
                new Tool { Name = "Channel Muncher", Kerf = 0.5 },
                new Tool { Name = "Custom", Kerf = 1, AllowUserToChange = true }
            };
        }

        /// <summary>
        /// Loads the tool list from the file at ToolsFilePath
        /// </summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(ToolsFilePath))
                {
                    LoadDefaultTools();
                    Save();
                }
                else
                {
                    var json = File.ReadAllText(ToolsFilePath);
                    var list = JsonConvert.DeserializeObject<List<Tool>>(json);
                    Tools = list;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void Save()
        {
            try
            {
                if (Tools == null)
                    return;

                var json = JsonConvert.SerializeObject(Tools, Formatting.Indented);
                File.WriteAllText(ToolsFilePath, json);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}