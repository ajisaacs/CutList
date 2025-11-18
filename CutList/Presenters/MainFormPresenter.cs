using CutList.Forms;
using CutList.Models;
using CutList.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            UpdateRunButtonState();
        }

        /// <summary>
        /// Handles the "Save" operation to save the current document to file.
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

            var defaultFileName = _currentDocument.LastFilePath == null
                ? "NewDocument.json"
                : Path.GetFileName(_currentDocument.LastFilePath);

            if (!_view.PromptSaveFile("Json File|*.json", defaultFileName, out string filePath))
                return;

            var saveResult = _documentService.Save(_currentDocument, filePath);

            if (saveResult.IsFailure)
            {
                _view.ShowError(saveResult.Error);
            }
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
            _view.ShowResults(packResult.Value.Bins.ToList(), fileName);
        }

        /// <summary>
        /// Handles creating a new document.
        /// </summary>
        public void NewDocument()
        {
            _currentDocument = new Document();
            _view.ClearData();
            UpdateRunButtonState();
        }

        /// <summary>
        /// Handles loading example data for testing.
        /// </summary>
        /// <param name="clearCurrentData">Whether to clear existing data first</param>
        public void LoadExampleData(bool clearCurrentData)
        {
            // Example data loading logic would go here
            // For now, just delegate to view if needed
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

                if (result == null) // Cancel
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
            var today = DateTime.Today;
            var year = today.Year.ToString();
            var month = today.Month.ToString().PadLeft(2, '0');
            var day = today.Day.ToString().PadLeft(2, '0');
            return $"Cut List {year}-{month}-{day}";
        }
    }
}
