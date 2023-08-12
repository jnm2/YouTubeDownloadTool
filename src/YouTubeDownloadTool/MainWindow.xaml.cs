using System;
using System.Windows;
using System.Windows.Controls;

namespace YouTubeDownloadTool;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Activated += OnShown;

        UrlTextBox.Focus();
    }

    private void OnShown(object? sender, EventArgs e)
    {
        Activated -= OnShown;

        if (UrlTextBox.IsFocused
            && string.IsNullOrWhiteSpace(UrlTextBox.Text)
            && Uri.TryCreate(Clipboard.GetText(), UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            UrlTextBox.UpdateBinding(TextBox.TextProperty, uri.ToString());
            UrlTextBox.SelectAll();
        }
    }

    private void OnBrowseButtonClick(object? sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select destination",
            UseDescriptionForTitle = true,
            SelectedPath = DestinationTextBox.Text,
        };

        if (dialog.ShowDialog(owner: this) == System.Windows.Forms.DialogResult.OK)
        {
            DestinationTextBox.UpdateBinding(TextBox.TextProperty, dialog.SelectedPath);
        }
    }
}