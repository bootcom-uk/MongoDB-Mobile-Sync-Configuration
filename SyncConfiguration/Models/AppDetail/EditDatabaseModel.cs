using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncConfiguration.Models.AppDetail
{
    public partial class EditDatabaseModel : ObservableObject
    {

        [ObservableProperty]
        private string databaseName;

        [ObservableProperty]
        private List<EditCollectionModel> collections;
    }
}
