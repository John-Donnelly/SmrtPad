using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SmrtPad.ViewModels
{
    public partial class EditorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _documentTitle = "Untitled";

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isModified = false;

        public EditorViewModel()
        {
        }

        [RelayCommand]
        public void NewDocument()
        {
            DocumentTitle = "Untitled";
            StatusMessage = "New document created.";
            IsModified = false;
        }

        [RelayCommand]
        public void UpdateStatus(string message)
        {
            StatusMessage = message;
        }
    }
}