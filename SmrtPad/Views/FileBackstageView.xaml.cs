using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Res = SmrtPad.Helpers.ResourceHelper;

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

    private bool _suppressSelectionEvent = true;

    public FileBackstageView()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0];
        _suppressSelectionEvent = false;
    }

    public void SetDocumentProperties(string fileName, int wordCount, int charCount, string encoding, bool isModified)
    {
        PropFileName.Text = fileName;
        PropWordCount.Text = wordCount.ToString("N0");
        PropCharCount.Text = charCount.ToString("N0");
        PropEncoding.Text = encoding;
        PropModified.Text = isModified ? Res.GetString("DocPropYes") : Res.GetString("DocPropNo");
    }

    public void SetRecentFiles(List<string> recentFiles)
    {
        RecentFilesList.Items.Clear();
        if (recentFiles.Count == 0)
        {
            RecentFilesList.Items.Add(new TextBlock { Text = Res.GetString("BackstageNoRecentFiles"), Opacity = 0.5 });
            return;
        }
        foreach (var path in recentFiles)
        {
            var item = new MenuFlyoutItem { Text = System.IO.Path.GetFileName(path), Tag = path };
            var btn = new Button
            {
                Content = System.IO.Path.GetFileName(path),
                Tag = path,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 6, 8, 6)
            };
            var tip = new ToolTip { Content = path };
            ToolTipService.SetToolTip(btn, tip);
            btn.Click += (s, e) =>
            {
                if (s is Button b && b.Tag is string filePath)
                    RecentFileRequested?.Invoke(this, filePath);
            };
            RecentFilesList.Items.Add(btn);
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressSelectionEvent)
            return;

        if (args.SelectedItem is not NavigationViewItem item)
            return;

        var tag = item.Tag as string;
        HeaderText.Text = tag is null ? Res.GetString("BackstageFile") : tag;
        RecentFilesPanel.Visibility = Visibility.Collapsed;
        DocPropertiesPanel.Visibility = Visibility.Collapsed;
        BodyText.Visibility = Visibility.Visible;

        switch (tag)
        {
            case "New":
                BodyText.Text = Res.GetString("BackstageNewDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                NewRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Open":
                BodyText.Text = Res.GetString("BackstageOpenDesc");
                RecentFilesPanel.Visibility = Visibility.Visible;
                OpenRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Save":
                BodyText.Text = Res.GetString("BackstageSaveDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "SaveAs":
                BodyText.Text = Res.GetString("BackstageSaveAsDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                SaveAsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Print":
                BodyText.Text = Res.GetString("BackstagePrintDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                PrintRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Options":
                BodyText.Text = Res.GetString("BackstageOptionsDesc");
                OptionsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Exit":
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return;
        }
    }
}
