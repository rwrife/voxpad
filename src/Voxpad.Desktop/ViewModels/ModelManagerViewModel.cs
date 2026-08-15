using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Voxpad.Core.Models;
using Voxpad.Desktop.Infrastructure;

namespace Voxpad.Desktop.ViewModels;

public sealed class ModelManagerViewModel : ViewModelBase
{
    private const string DefaultFirstRunGuidance =
        "No models installed yet. Start by downloading 'base' for a good speed/accuracy balance.";

    private readonly IModelStore modelStore;

    private bool isRefreshing;
    private string? statusMessage;
    private bool hasFirstRunGuidance;

    public ModelManagerViewModel(IModelStore modelStore)
    {
        this.modelStore = modelStore ?? throw new ArgumentNullException(nameof(modelStore));

        var modelRows = modelStore.ListAvailableModels()
            .OrderBy(m => m.SizeBytes)
            .Select(model => new ModelItemViewModel(
                model,
                onDownload: DownloadModelAsync,
                onCancel: CancelDownload,
                onSelect: SelectModelAsync,
                onDelete: DeleteModelAsync));

        Models = new ObservableCollection<ModelItemViewModel>(modelRows);
        foreach (var item in Models)
        {
            item.PropertyChanged += OnModelItemPropertyChanged;
        }

        Models.CollectionChanged += OnModelsCollectionChanged;

        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public ObservableCollection<ModelItemViewModel> Models { get; }

    public ICommand RefreshCommand { get; }

    public bool IsRefreshing
    {
        get => isRefreshing;
        private set => SetProperty(ref isRefreshing, value);
    }

    public string? StatusMessage
    {
        get => statusMessage;
        private set
        {
            if (SetProperty(ref statusMessage, value))
            {
                RaisePropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasFirstRunGuidance
    {
        get => hasFirstRunGuidance;
        private set => SetProperty(ref hasFirstRunGuidance, value);
    }

    public string FirstRunGuidance => DefaultFirstRunGuidance;

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        try
        {
            IsRefreshing = true;
            StatusMessage = null;

            var installed = await modelStore.ListInstalledModelsAsync();
            var selected = await modelStore.GetSelectedModelIdAsync();

            var installedById = installed
                .Select(i => i.Model.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in Models)
            {
                var isInstalled = installedById.Contains(row.ModelId);
                var isSelected = isInstalled && string.Equals(row.ModelId, selected, StringComparison.OrdinalIgnoreCase);
                row.SetInstalledSelected(isInstalled, isSelected);
            }

            HasFirstRunGuidance = installed.Count == 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not refresh model list: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task DownloadModelAsync(ModelItemViewModel row)
    {
        if (!row.CanDownload)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        row.BeginDownload(cts);
        StatusMessage = null;

        try
        {
            var progress = new Progress<double>(row.UpdateProgress);
            await modelStore.DownloadModelAsync(row.ModelId, progress, cts.Token);
            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            row.MarkDownloadCanceled();
        }
        catch (Exception ex)
        {
            row.MarkDownloadFailed($"Download failed: {ex.Message}");
        }
        finally
        {
            row.EndBusyState();
        }
    }

    private void CancelDownload(ModelItemViewModel row)
    {
        row.CancelDownload();
    }

    private async Task SelectModelAsync(ModelItemViewModel row)
    {
        if (!row.CanSelect)
        {
            return;
        }

        StatusMessage = null;

        try
        {
            await modelStore.SelectModelAsync(row.ModelId);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            row.MarkActionFailure("Select", ex);
        }
    }

    private async Task DeleteModelAsync(ModelItemViewModel row)
    {
        if (!row.CanDelete)
        {
            return;
        }

        StatusMessage = null;

        try
        {
            await modelStore.DeleteModelAsync(row.ModelId);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            row.MarkActionFailure("Delete", ex);
        }
    }

    private void OnModelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ModelItemViewModel>())
            {
                item.PropertyChanged += OnModelItemPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ModelItemViewModel>())
            {
                item.PropertyChanged -= OnModelItemPropertyChanged;
            }
        }
    }

    private void OnModelItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModelItemViewModel.IsInstalled))
        {
            HasFirstRunGuidance = Models.All(m => !m.IsInstalled);
        }
    }
}
