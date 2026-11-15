using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncConfiguration.Models.Config
{
    public partial class AppURLInfo : ObservableObject
    {

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string url;

        [ObservableProperty]
        private string audience;

    }
}
