using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncConfiguration.Models.Config
{
    public partial class AppSettings : ObservableObject
    {

        [ObservableProperty]
        AppURLInfo[] baseAppUrl;  

        [ObservableProperty]
        Syncfusion syncfusion;

        [ObservableProperty]
        Sentry sentry;

    }
}
