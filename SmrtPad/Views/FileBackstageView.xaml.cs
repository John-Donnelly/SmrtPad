using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SmrtPad.Views;

public sealed partial class FileBackstageView : UserControl
{
    public event EventHandler? NewRequested;
    public event EventHandler? OpenRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler? PrintRequested;
    public event EventHandler? OptionsRequested;
    public event EventHandler? ExitRequested;

    private bool _suppressSelectionEvent;

    public FileBackstageView()
    {
        InitializeComponent();
        _suppressSelectionEvent = true;
        Nav.SelectedItem = Nav.MenuItems[0];
        HeaderText.Text = "New";
        BodyText.Text = "Create a new document.";
        _suppressSelectionEvent = false;
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressSelectionEvent || args.SelectedItem is not NavigationViewItem item)
            return;

        var tag = item.Tag as string;
        HeaderText.Text = tag is null ? "File" : tag;

        switch (tag)
        {
            case "New":
                BodyText.Text = "Create a new document.";
                NewRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Open":
                BodyText.Text = "Open an existing document.";
                OpenRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Save":
                BodyText.Text = "Save changes to the current document.";
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "SaveAs":
                BodyText.Text = "Save the current document under a new name.";
                SaveAsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Print":
                BodyText.Text = "Print the current document.";
                PrintRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Options":
                BodyText.Text = "Configure application options.";
                OptionsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Exit":
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return;
        }
    }
}
