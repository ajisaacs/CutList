using CutList.Core;
using CutList.Models;

namespace CutList.Presenters
{
    /// <summary>
    /// Interface defining the contract for the main view in the MVP pattern.
    /// The view is responsible only for UI rendering and user interaction,
    /// delegating all business logic to the presenter.
    /// </summary>
    public interface IMainView
    {
        /// <summary>
        /// Gets the current list of parts to nest from the view.
        /// </summary>
        List<PartInputItem> Parts { get; }

        /// <summary>
        /// Gets the current list of stock bins from the view.
        /// </summary>
        List<BinInputItem> StockBins { get; }

        /// <summary>
        /// Gets the currently selected cutting tool from the view.
        /// </summary>
        Tool SelectedTool { get; }

        /// <summary>
        /// Gets the currently selected material shape from the view.
        /// </summary>
        string? SelectedMaterialShape { get; }

        /// <summary>
        /// Displays an error message to the user.
        /// </summary>
        void ShowError(string message);

        /// <summary>
        /// Displays a warning message to the user.
        /// </summary>
        void ShowWarning(string message);

        /// <summary>
        /// Displays an information message to the user.
        /// </summary>
        void ShowInfo(string message);

        /// <summary>
        /// Asks the user a yes/no question.
        /// </summary>
        /// <returns>True if user selected yes, false otherwise</returns>
        bool AskYesNo(string question, string title);

        /// <summary>
        /// Asks the user a yes/no/cancel question.
        /// </summary>
        /// <returns>True if yes, false if no, null if cancel</returns>
        bool? AskYesNoCancel(string question, string title);

        /// <summary>
        /// Prompts the user to select a file to open.
        /// </summary>
        /// <param name="filter">File filter (e.g., "Json File|*.json")</param>
        /// <param name="filePath">Output parameter with selected file path</param>
        /// <returns>True if user selected a file, false if cancelled</returns>
        bool PromptOpenFile(string filter, out string filePath);

        /// <summary>
        /// Prompts the user to select a file to save.
        /// </summary>
        /// <param name="filter">File filter (e.g., "Json File|*.json")</param>
        /// <param name="defaultFileName">Default file name</param>
        /// <param name="filePath">Output parameter with selected file path</param>
        /// <returns>True if user selected a file, false if cancelled</returns>
        bool PromptSaveFile(string filter, string defaultFileName, out string filePath);

        /// <summary>
        /// Loads document data into the view.
        /// </summary>
        void LoadDocumentData(List<PartInputItem> parts, List<BinInputItem> stockBins);

        /// <summary>
        /// Shows the results form with the packing results.
        /// </summary>
        /// <param name="bins">The packed bins to display</param>
        /// <param name="fileName">Default filename for saving</param>
        /// <param name="cutMethod">The cutting method/tool name</param>
        /// <param name="materialShape">The material shape (optional)</param>
        void ShowResults(List<Bin> bins, string fileName, string cutMethod, string? materialShape = null);

        /// <summary>
        /// Updates the enabled state of the run button.
        /// </summary>
        void UpdateRunButtonState(bool enabled);

        /// <summary>
        /// Clears all data in the view.
        /// </summary>
        void ClearData();

        /// <summary>
        /// Updates the window title to reflect the current document state.
        /// </summary>
        /// <param name="fileName">The file name to display, or null for a new document</param>
        void UpdateWindowTitle(string? fileName);
    }
}
