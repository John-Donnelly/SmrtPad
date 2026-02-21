using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SmrtPad.Services
{
    public class DialogService : IDialogService
    {
        private readonly Func<XamlRoot> _xamlRootProvider;

        public DialogService(Func<XamlRoot> xamlRootProvider)
        {
            _xamlRootProvider = xamlRootProvider;
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = _xamlRootProvider()
            };
            await dialog.ShowAsync();
        }

        public async Task<SavePromptResult> ShowSavePromptAsync(string documentTitle)
        {
            var dialog = new ContentDialog
            {
                Title = "Unsaved Changes",
                Content = $"Do you want to save changes to {documentTitle}?",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Don't Save",
                CloseButtonText = "Cancel",
                XamlRoot = _xamlRootProvider()
            };

            var result = await dialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => SavePromptResult.Save,
                ContentDialogResult.Secondary => SavePromptResult.DontSave,
                _ => SavePromptResult.Cancel
            };
        }
    }
}
