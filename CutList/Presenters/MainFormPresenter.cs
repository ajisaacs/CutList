using CutList.Models;
using CutList.Services;

namespace CutList.Presenters
{
    /// <summary>
    /// Presenter for the main form following the MVP pattern.
    /// Contains all business logic and orchestration, keeping the view focused on UI only.
    /// </summary>
    public class MainFormPresenter
    {
        private readonly IMainView _view;
        private readonly CutListService _cutListService;
        private readonly DocumentService _documentService;
        private Document _currentDocument;
        private int _documentCounter = 0;

        public MainFormPresenter(IMainView view, CutListService cutListService, DocumentService documentService)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _cutListService = cutListService ?? throw new ArgumentNullException(nameof(cutListService));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _currentDocument = new Document();
        }

        /// <summary>
        /// Handles the "Open" operation to load a document from file.
        /// </summary>
        public void OpenDocument()
        {
            if (!_view.PromptOpenFile("Json File|*.json", out string filePath))
                return;

            var loadResult = _documentService.Load(filePath);

            if (loadResult.IsFailure)
            {
                _view.ShowError(loadResult.Error);
                return;
            }

            _currentDocument = loadResult.Value;
            _view.LoadDocumentData(_currentDocument.PartsToNest, _currentDocument.StockBins);
            _view.UpdateWindowTitle(Path.GetFileName(filePath));
            UpdateRunButtonState();
        }

        /// <summary>
        /// Handles the "Save" operation. If the document has a known path, saves directly.
        /// Otherwise, prompts for a file location (same as Save As).
        /// </summary>
        public void SaveDocument()
        {
            SyncDocumentFromView();

            var validationResult = _documentService.Validate(_currentDocument);
            if (validationResult.IsFailure)
            {
                _view.ShowWarning(validationResult.Error);
                return;
            }

            // If we have a known path, save directly without prompting
            if (!string.IsNullOrEmpty(_currentDocument.LastFilePath))
            {
                SaveToPath(_currentDocument.LastFilePath);
                return;
            }

            // No known path - prompt for location (same as Save As)
            SaveDocumentAs();
        }

        /// <summary>
        /// Handles the "Save As" operation. Always prompts for a file location.
        /// </summary>
        public void SaveDocumentAs()
        {
            SyncDocumentFromView();

            var validationResult = _documentService.Validate(_currentDocument);
            if (validationResult.IsFailure)
            {
                _view.ShowWarning(validationResult.Error);
                return;
            }

            var defaultFileName = string.IsNullOrEmpty(_currentDocument.LastFilePath)
                ? GenerateDefaultFileName()
                : Path.GetFileName(_currentDocument.LastFilePath);

            if (!_view.PromptSaveFile("Json File|*.json", defaultFileName, out string filePath))
                return;

            SaveToPath(filePath);
        }

        private void SaveToPath(string filePath)
        {
            var saveResult = _documentService.Save(_currentDocument, filePath);

            if (saveResult.IsFailure)
            {
                _view.ShowError(saveResult.Error);
                return;
            }

            _currentDocument.LastFilePath = filePath;
            _view.UpdateWindowTitle(Path.GetFileName(filePath));
        }

        private string GenerateDefaultFileName()
        {
            _documentCounter++;
            return $"CutList_{_documentCounter}.json";
        }

        /// <summary>
        /// Handles the "Run" operation to execute the cut list optimization.
        /// </summary>
        public void Run()
        {
            var parts = _view.Parts;
            var stockBins = _view.StockBins;
            var cutTool = _view.SelectedTool;

            var packResult = _cutListService.Pack(parts, stockBins, cutTool);

            if (packResult.IsFailure)
            {
                _view.ShowError(packResult.Error);
                return;
            }

            var fileName = GetResultsSaveName();
            var cutMethod = cutTool?.Name ?? "Unknown";
            var materialShape = _view.SelectedMaterialShape;
            _view.ShowResults(packResult.Value.Bins.ToList(), fileName, cutMethod, materialShape);
        }

        /// <summary>
        /// Handles creating a new document.
        /// </summary>
        public void NewDocument()
        {
            _currentDocument = new Document();
            _view.ClearData();
            _view.UpdateWindowTitle(null);
            UpdateRunButtonState();
        }

        /// <summary>
        /// Handles loading example data for testing.
        /// </summary>
        /// <param name="clearCurrentData">Whether to clear existing data first</param>
        public void LoadExampleData(bool clearCurrentData)
        {
            const int PartCount = 25;
            const double Min = 1;
            const double Max = 60;

            if (clearCurrentData)
            {
                _view.ClearData();
            }

            var parts = new List<PartInputItem>();
            var bins = new List<BinInputItem>();
            var random = new Random();

            for (int i = 0; i < PartCount; i++)
            {
                var length = GetRandomLength(random, Min, Max);

                parts.Add(new PartInputItem
                {
                    Name = $"Part {i + 1}",
                    LengthInputValue = length.ToString(),
                    Quantity = random.Next(1, 100)
                });
            }

            bins.Add(new BinInputItem
            {
                LengthInputValue = "144\"",
                Quantity = 9999
            });

            _view.LoadDocumentData(parts, bins);
            UpdateRunButtonState();
        }

        private double GetRandomLength(Random random, double min, double max)
        {
            return Math.Round(random.NextDouble() * (max - min) + min, 2);
        }

        /// <summary>
        /// Validates the current view state and updates button states accordingly.
        /// </summary>
        public void UpdateRunButtonState()
        {
            var parts = _view.Parts;
            var stockBins = _view.StockBins;

            bool isValid = parts != null && parts.Any(i => i.Length > 0 && i.Quantity > 0) &&
                          stockBins != null && stockBins.Any(i => i.Length > 0 && (i.Quantity > 0 || i.Quantity == -1));

            _view.UpdateRunButtonState(isValid);
        }

        /// <summary>
        /// Handles the "Load Example Data" button click.
        /// </summary>
        public void OnLoadExampleDataRequested()
        {
            var parts = _view.Parts;
            var stockBins = _view.StockBins;

            if (parts.Count > 0 || stockBins.Count > 0)
            {
                var result = _view.AskYesNoCancel("Are you sure you want to clear the current data?", "Clear Data");

                if (result == null || !result.Value)
                    return;

                LoadExampleData(result.Value);
            }
            else
            {
                LoadExampleData(true);
            }
        }

        private void SyncDocumentFromView()
        {
            if (_currentDocument == null)
            {
                _currentDocument = new Document();
            }

            _currentDocument.PartsToNest = _view.Parts;
            _currentDocument.StockBins = _view.StockBins;
            _currentDocument.Tool = _view.SelectedTool;
        }

        private string GetResultsSaveName()
        {
            // Use document name if available, otherwise generate one
            if (!string.IsNullOrEmpty(_currentDocument.LastFilePath))
            {
                var docName = Path.GetFileNameWithoutExtension(_currentDocument.LastFilePath);
                return docName;
            }

            return $"CutList_{_documentCounter}";
        }
    }
}
