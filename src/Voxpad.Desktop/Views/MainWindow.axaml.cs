using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Voxpad.Desktop.ViewModels;

namespace Voxpad.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Media.Dispose();
        }

        base.OnClosed(e);
    }

    private async void OpenMedia_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open audio or video",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio and video")
                {
                    Patterns =
                    [
                        "*.wav", "*.mp3", "*.m4a", "*.aac", "*.flac", "*.ogg",
                        "*.mp4", "*.mov", "*.mkv", "*.webm", "*.avi"
                    ]
                },
                FilePickerFileTypes.All
            ]
        });

        var mediaPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            return;
        }

        var loaded = await vm.Media.LoadMediaAsync(mediaPath, discardUnsavedChanges: false);
        if (!loaded && vm.Media.RequiresDiscardConfirmation && await ConfirmDiscardAsync())
        {
            await vm.Media.LoadMediaAsync(mediaPath, discardUnsavedChanges: true);
        }
    }

    private async void SeekSegment_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is Button { DataContext: TranscriptSegmentViewModel segment })
        {
            await vm.Media.SeekToSegmentAsync(segment);
        }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        var confirmed = false;
        var dialog = new Window
        {
            Title = "Discard transcript edits?",
            Width = 440,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var discardButton = new Button { Content = "Discard edits and load", HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Right };
        discardButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(18),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Loading another media file will restore the last saved timestamped transcript. Continue?",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, discardButton }
                }
            }
        };

        await dialog.ShowDialog(this);
        return confirmed;
    }
}
