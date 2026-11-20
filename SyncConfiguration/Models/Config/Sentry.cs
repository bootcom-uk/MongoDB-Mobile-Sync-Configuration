using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncConfiguration.Models.Config
{
    public partial class Sentry : ObservableObject
    {

        [ObservableProperty]
        string dsn;

    }
}
