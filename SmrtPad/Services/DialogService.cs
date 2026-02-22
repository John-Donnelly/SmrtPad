using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Res = SmrtPad.Helpers.ResourceHelper;

namespace SmrtPad.Services
{
    public class DialogService : IDialogService
    {
        private readonly Func<XamlRoot> _xamlRootProvider;

        public DialogService()
        {
            _xamlRootProvider = () => App.MainWindow.Content.XamlRoot;
        }

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
                CloseButtonText = Res.GetString("DlgOK"),
                XamlRoot = _xamlRootProvider()
            };
            await dialog.ShowAsync();
        }

        public async Task<SavePromptResult> ShowSavePromptAsync(string documentTitle)
        {
            var dialog = new ContentDialog
            {
                Title = Res.GetString("DlgUnsavedChanges"),
                Content = Res.GetFormatted("DlgSaveChangesMessage", documentTitle),
                PrimaryButtonText = Res.GetString("DlgSave"),
                SecondaryButtonText = Res.GetString("DlgDontSave"),
                CloseButtonText = Res.GetString("DlgCancel"),
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
