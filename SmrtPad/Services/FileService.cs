using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SmrtPad.Services
{
    public class FileService : IFileService
    {
        private readonly Func<Window> _windowProvider;

        public FileService(Func<Window> windowProvider)
        {
            _windowProvider = windowProvider;
        }

        public async Task<StorageFile?> PickOpenFileAsync(string[] fileTypes)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_windowProvider()));
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            foreach (var ft in fileTypes)
                picker.FileTypeFilter.Add(ft);

            return await picker.PickSingleFileAsync();
        }

        public async Task<StorageFile?> PickSaveFileAsync(string suggestedName, string defaultExtension)
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_windowProvider()));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Rich Text Format", new List<string>() { ".rtf" });
            picker.FileTypeChoices.Add("Text Document", new List<string>() { ".txt" });
            picker.SuggestedFileName = suggestedName;

            return await picker.PickSaveFileAsync();
        }

        public async Task<StorageFile?> GetFileFromPathAsync(string path)
        {
            return await StorageFile.GetFileFromPathAsync(path);
        }
    }
}
