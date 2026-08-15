using System.Globalization;
using System.Windows.Input;
using Voxpad.Core.Models;
using Voxpad.Desktop.Infrastructure;

namespace Voxpad.Desktop.ViewModels;

public sealed class ModelItemViewModel : ViewModelBase
{
    private bool isInstalled;
    private bool isSelected;
    private bool isBusy;
    private double downloadProgress;
    private string statusText = "Not installed";
    private CancellationTokenSource? downloadCancellation;

    public ModelItemViewModel(
        WhisperModelInfo model,
        Func<ModelItemViewModel, Task> onDownload,
        Action<ModelItemViewModel> onCancel,
        Func<ModelItemViewModel, Task> onSelect,
        Func<ModelItemViewModel, Task> onDelete)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));

        DownloadCommand = new AsyncCommand(() => onDownload(this));
        CancelCommand = new RelayCommand(() => onCancel(this));
        SelectCommand = new AsyncCommand(() => onSelect(this));
        DeleteCommand = new AsyncCommand(() => onDelete(this));
    }

    public WhisperModelInfo Model { get; }

    public string ModelId => Model.Id;

    public string DisplayName => Model.DisplayName;

    public string Language => Model.Language;

    public string SizeLabel => $"{Model.SizeBytes / (1024d * 1024d):0.#} MB";

    public bool IsInstalled
    {
        get => isInstalled;
        private set
        {
            if (SetProperty(ref isInstalled, value))
            {
                RaiseActionStateChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => isSelected;
        private set
        {
            if (SetProperty(ref isSelected, value))
            {
                RaiseActionStateChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaiseActionStateChanged();
            }
        }
    }

    public double DownloadProgress
    {
        get => downloadProgress;
        private set
        {
            SetProperty(ref downloadProgress, value);
        }
    }

    public string DownloadPercentLabel =>
        IsBusy ? $"{Math.Round(DownloadProgress * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%" : string.Empty;

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public bool CanDownload => !IsInstalled && !IsBusy;

    public bool CanCancel => IsBusy;

    public bool CanSelect => IsInstalled && !IsSelected && !IsBusy;

    public bool CanDelete => IsInstalled && !IsBusy;

    public ICommand DownloadCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand SelectCommand { get; }

    public ICommand DeleteCommand { get; }

    internal void SetInstalledSelected(bool installed, bool selected)
    {
        IsInstalled = installed;
        IsSelected = installed && selected;

        if (!IsBusy)
        {
            DownloadProgress = 0d;
            RaisePropertyChanged(nameof(DownloadPercentLabel));

            StatusText = IsSelected
                ? "Selected"
                : IsInstalled
                    ? "Installed"
                    : "Not installed";
        }
    }

    internal void BeginDownload(CancellationTokenSource cancellation)
    {
        downloadCancellation = cancellation;
        IsBusy = true;
        DownloadProgress = 0d;
        StatusText = "Downloading...";
        RaisePropertyChanged(nameof(DownloadPercentLabel));
    }

    internal void UpdateProgress(double progress)
    {
        DownloadProgress = Math.Clamp(progress, 0d, 1d);
        RaisePropertyChanged(nameof(DownloadPercentLabel));
    }

    internal void MarkDownloadCanceled()
    {
        StatusText = "Download canceled";
    }

    internal void MarkDownloadFailed(string message)
    {
        StatusText = message;
    }

    internal void EndBusyState()
    {
        IsBusy = false;
        downloadCancellation = null;

        if (IsSelected)
        {
            StatusText = "Selected";
        }
        else if (IsInstalled)
        {
            StatusText = "Installed";
        }
        else
        {
            DownloadProgress = 0d;
            RaisePropertyChanged(nameof(DownloadPercentLabel));
        }
    }

    internal void MarkActionFailure(string action, Exception ex)
    {
        StatusText = $"{action} failed: {ex.Message}";
    }

    internal void CancelDownload()
    {
        downloadCancellation?.Cancel();
    }

    private void RaiseActionStateChanged()
    {
        RaisePropertyChanged(nameof(CanDownload));
        RaisePropertyChanged(nameof(CanCancel));
        RaisePropertyChanged(nameof(CanSelect));
        RaisePropertyChanged(nameof(CanDelete));
    }
}
