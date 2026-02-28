using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmrtPad.Helpers;
using SmrtPad.Models;
using Res = SmrtPad.Helpers.ResourceHelper;

namespace SmrtPad.Views;

public sealed partial class FileBackstageView : UserControl
{
    public event EventHandler? NewRequested;
    public event EventHandler? OpenRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler? PrintRequested;
    public event EventHandler? ExportPdfRequested;
    public event EventHandler? ExportDocxRequested;
    public event EventHandler? OneDriveRequested;
    public event EventHandler? OptionsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<string>? RecentFileRequested;
    public event EventHandler<DocumentTemplate>? TemplateRequested;

    private bool _suppressSelectionEvent = true;

    public FileBackstageView()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0];
        _suppressSelectionEvent = false;
        PopulateTemplates();
    }

    private void PopulateTemplates()
    {
        foreach (var template in DocumentTemplates.All)
        {
            var card = new Button
            {
                Tag = template,
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding      = new Thickness(12),
                CornerRadius = new CornerRadius(8),
            };
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock
            {
                Text         = template.DisplayName,
                FontWeight   = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(new TextBlock
            {
                Text         = template.Description,
                Opacity      = 0.7,
                TextWrapping = TextWrapping.Wrap,
                FontSize     = 12,
            });
            card.Content = panel;
            card.Click  += (s, _) =>
            {
                if (s is Button b && b.Tag is DocumentTemplate t)
                    TemplateRequested?.Invoke(this, t);
            };
            TemplateListPanel.Children.Add(card);
        }
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

    private void NavItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not NavigationViewItem item)
            return;

        // Select the item so the NavigationView visually highlights it
        _suppressSelectionEvent = true;
        Nav.SelectedItem = item;
        _suppressSelectionEvent = false;

        ShowPaneForTag(item.Tag as string);
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressSelectionEvent)
            return;

        if (args.SelectedItem is not NavigationViewItem item)
            return;

        var tag = item.Tag as string;
        ShowPaneForTag(tag);
        ExecuteActionForTag(tag);
    }

    /// <summary>
    /// Shows the appropriate content pane for the given navigation tag.
    /// </summary>
    private void ShowPaneForTag(string? tag)
    {
        HeaderText.Text = tag is null ? Res.GetString("BackstageFile") : tag;
        RecentFilesPanel.Visibility   = Visibility.Collapsed;
        DocPropertiesPanel.Visibility = Visibility.Collapsed;
        TemplatePicker.Visibility     = Visibility.Collapsed;
        BodyText.Visibility           = Visibility.Visible;

        switch (tag)
        {
            case "New":
                BodyText.Text = Res.GetString("BackstageNewDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "Templates":
                BodyText.Text = Res.GetString("BackstageTemplatesDesc");
                TemplatePicker.Visibility = Visibility.Visible;
                break;
            case "Open":
                BodyText.Text = Res.GetString("BackstageOpenDesc");
                RecentFilesPanel.Visibility = Visibility.Visible;
                break;
            case "Save":
                BodyText.Text = Res.GetString("BackstageSaveDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "SaveAs":
                BodyText.Text = Res.GetString("BackstageSaveAsDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "Print":
                BodyText.Text = Res.GetString("BackstagePrintDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "ExportPdf":
                BodyText.Text = Res.GetString("BackstageExportPdfDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "ExportDocx":
                BodyText.Text = Res.GetString("BackstageExportDocxDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "OneDrive":
                BodyText.Text = Res.GetString("BackstageSaveOneDriveDesc");
                DocPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "Options":
                BodyText.Text = Res.GetString("BackstageOptionsDesc");
                break;
            case "Exit":
                BodyText.Text = Res.GetString("BackstageExitDesc");
                break;
        }
    }

    /// <summary>
    /// Executes the action associated with the given navigation tag (on click).
    /// </summary>
    private void ExecuteActionForTag(string? tag)
    {
        switch (tag)
        {
            case "New":
                NewRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Open":
                OpenRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Save":
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "SaveAs":
                SaveAsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Print":
                PrintRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "ExportPdf":
                ExportPdfRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "ExportDocx":
                ExportDocxRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "OneDrive":
                OneDriveRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Options":
                OptionsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Exit":
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
}
