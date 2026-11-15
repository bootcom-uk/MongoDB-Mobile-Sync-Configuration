using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncConfiguration.Models.Config
{
    public partial class Syncfusion : ObservableObject
    {

        /// <summary>
        /// Specifies the licence key being used for Syncfusion products
        /// </summary>
        /// <remarks>In order to use Syncfusion products</remarks>
        [ObservableProperty]
        string licenceKey;

    }
}
