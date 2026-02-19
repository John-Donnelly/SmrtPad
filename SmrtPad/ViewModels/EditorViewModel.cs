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

        [ObservableProperty]
        private string _fontFamily = "Segoe UI";

        [ObservableProperty]
        private double _fontSize = 11.0;

        [ObservableProperty]
        private bool _isBold = false;

        [ObservableProperty]
        private bool _isItalic = false;

        [ObservableProperty]
        private bool _isUnderline = false;

        [ObservableProperty]
        private bool _isStrikethrough = false;

        [ObservableProperty]
        private bool _isSubscript = false;

        [ObservableProperty]
        private bool _isSuperscript = false;

        [ObservableProperty]
        private string _alignment = "Left";

        [ObservableProperty]
        private bool _isBullets = false;

        public EditorViewModel()
        {
        }

        [RelayCommand]
        public void NewDocument()
        {
            DocumentTitle = "Untitled";
            StatusMessage = "New document created.";
            IsModified = false;
            FontFamily = "Segoe UI";
            FontSize = 11.0;
            IsBold = false;
            IsItalic = false;
            IsUnderline = false;
            IsStrikethrough = false;
            IsSubscript = false;
            IsSuperscript = false;
            Alignment = "Left";
            IsBullets = false;
        }

        [RelayCommand]
        public void UpdateStatus(string message)
        {
            StatusMessage = message;
        }

        [RelayCommand]
        public void ToggleBold()
        {
            IsBold = !IsBold;
        }

        [RelayCommand]
        public void ToggleItalic()
        {
            IsItalic = !IsItalic;
        }

        [RelayCommand]
        public void ToggleUnderline()
        {
            IsUnderline = !IsUnderline;
        }

        [RelayCommand]
        public void ToggleStrikethrough()
        {
            IsStrikethrough = !IsStrikethrough;
        }

        [RelayCommand]
        public void ToggleSubscript()
        {
            IsSubscript = !IsSubscript;
            if (IsSubscript) IsSuperscript = false;
        }

        [RelayCommand]
        public void ToggleSuperscript()
        {
            IsSuperscript = !IsSuperscript;
            if (IsSuperscript) IsSubscript = false;
        }

        [RelayCommand]
        public void SetAlignment(string alignment)
        {
            Alignment = alignment;
        }

        [RelayCommand]
        public void ToggleBullets()
        {
            IsBullets = !IsBullets;
        }
    }
}