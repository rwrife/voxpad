using Avalonia.Controls;
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
}
