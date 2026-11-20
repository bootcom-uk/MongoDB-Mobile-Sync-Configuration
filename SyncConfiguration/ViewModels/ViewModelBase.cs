using CommunityToolkit.Mvvm.ComponentModel;
using SyncConfiguration.Services;

namespace SyncConfiguration.ViewModels
{
    public abstract partial class ViewModelBase : ObservableObject, INavigationAware
    {

        public readonly InternalSettingsService InternalSettingsService;

        public readonly INavigationService NavigationService;

        public ViewModelBase(InternalSettingsService internalSettingsService, INavigationService navigationService)
        {
            InternalSettingsService = internalSettingsService;
            NavigationService = navigationService;

            IsProcessing = false;
        }

        public virtual void OnNavigatedFrom(INavigationParameters parameters) { }

        public virtual void OnNavigatedTo(INavigationParameters parameters) { }

        [ObservableProperty]
        bool isProcessing;

        [ObservableProperty]
        string? processingMessage;

        [ObservableProperty]
        byte popupHeight;

        [ObservableProperty]
        byte popupWidth;

    }
}
