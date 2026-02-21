using System;
using System.Collections.Generic;
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
    public event EventHandler<string>? RecentFileRequested;

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
                ShowDefaultPanel("Create a new document.");
                NewRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Open":
                ShowOpenPanel();
                break;
            case "Save":
                ShowDefaultPanel("Save changes to the current document.");
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "SaveAs":
                ShowDefaultPanel("Save the current document under a new name.");
                SaveAsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Print":
                ShowDefaultPanel("Print the current document.");
                PrintRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Options":
                ShowDefaultPanel("Configure application options.");
                OptionsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Exit":
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return;
        }
    }

    public void SetRecentFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            RecentFilesList.Visibility = Visibility.Collapsed;
            RecentFilesHeader.Visibility = Visibility.Collapsed;
            NoRecentFilesText.Visibility = Visibility.Visible;
        }
        else
        {
            RecentFilesList.ItemsSource = paths;
            RecentFilesList.Visibility = Visibility.Visible;
            RecentFilesHeader.Visibility = Visibility.Visible;
            NoRecentFilesText.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowDefaultPanel(string bodyText)
    {
        BodyText.Text = bodyText;
        BodyText.Visibility = Visibility.Visible;
        OpenPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowOpenPanel()
    {
        HeaderText.Text = "Open";
        BodyText.Visibility = Visibility.Collapsed;
        OpenPanel.Visibility = Visibility.Visible;
    }

    private void BrowseForFile_Click(object sender, RoutedEventArgs e)
    {
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RecentFile_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string path)
            RecentFileRequested?.Invoke(this, path);
    }
}
