using System.Threading.Tasks;

namespace SmrtPad.Services
{
    public interface IDialogService
    {
        Task ShowErrorAsync(string title, string message);
        Task<SavePromptResult> ShowSavePromptAsync(string documentTitle);
    }

    public enum SavePromptResult
    {
        Save,
        DontSave,
        Cancel
    }
}
