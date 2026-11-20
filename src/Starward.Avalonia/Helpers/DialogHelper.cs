using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;

namespace Starward.Helpers;

public static class DialogHelper
{

    public static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;


    public static async Task ShowMessageAsync(string title, string message)
    {
        Window? owner = MainWindow;
        Window window = CreateWindow(title, 420);
        StackPanel panel = new() { Spacing = 18, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        Button ok = new() { Content = Localization.Get("SelectDirectoryPage_OK"), HorizontalAlignment = HorizontalAlignment.Right, Classes = { "cta" } };
        ok.Click += (_, _) => window.Close();
        panel.Children.Add(ok);
        window.Content = panel;
        if (owner is not null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
    }


    public static async Task<bool> ShowConfirmAsync(string title, string message)
    {
        Window? owner = MainWindow;
        Window window = CreateWindow(title, 420);
        StackPanel panel = new() { Spacing = 18, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        bool result = false;
        Button cancel = new() { Content = Localization.Get("Common_Cancel") };
        cancel.Click += (_, _) => window.Close();
        Button ok = new() { Content = Localization.Get("SelectDirectoryPage_OK"), Classes = { "cta" } };
        ok.Click += (_, _) => { result = true; window.Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        window.Content = panel;
        if (owner is not null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
        return result;
    }


    public static async Task<string?> ShowInputAsync(string title, string message, string? initial = null)
    {
        Window? owner = MainWindow;
        Window window = CreateWindow(title, 520);
        StackPanel panel = new() { Spacing = 12, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        TextBox textBox = new() { Text = initial ?? "", MinHeight = 38 };
        panel.Children.Add(textBox);
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        string? result = null;
        Button cancel = new() { Content = Localization.Get("Common_Cancel") };
        cancel.Click += (_, _) => window.Close();
        Button ok = new() { Content = Localization.Get("SelectDirectoryPage_OK"), Classes = { "cta" } };
        ok.Click += (_, _) => { result = textBox.Text; window.Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.Opened += (_, _) => textBox.Focus();
        if (owner is not null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
        return result;
    }


    public static async Task<string?> PickFolderAsync(string title, string? startFolder = null)
    {
        Window? owner = MainWindow;
        TopLevel? topLevel = owner is null ? null : TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            return null;
        }
        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(startFolder) && System.IO.Directory.Exists(startFolder))
        {
            start = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startFolder);
        }
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }


    public static async Task<string?> PickFileAsync(string title, string? startFolder = null, params string[] patterns)
    {
        Window? owner = MainWindow;
        TopLevel? topLevel = owner is null ? null : TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            return null;
        }
        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(startFolder) && System.IO.Directory.Exists(startFolder))
        {
            start = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startFolder);
        }
        System.Collections.Generic.List<FilePickerFileType> types = new();
        if (patterns.Length > 0)
        {
            types.Add(new FilePickerFileType(Localization.Get("Avalonia_Dialog_Files")) { Patterns = patterns });
        }
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter = types.Count > 0 ? types : null,
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }


    public static async Task<string?> SaveFileAsync(string title, string suggestedName, params string[] patterns)
    {
        Window? owner = MainWindow;
        TopLevel? topLevel = owner is null ? null : TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            return null;
        }
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = patterns }],
        });
        return file?.Path.LocalPath;
    }


    private static Window CreateWindow(string title, double width)
    {
        return new Window
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#25252C")),
        };
    }

}
