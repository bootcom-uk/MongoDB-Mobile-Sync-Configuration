using CommunityToolkit.Mvvm.ComponentModel;
using MongoDB.Sync.Models;
using System.Collections.ObjectModel;

namespace SyncConfiguration.Models.AppDetail
{
    public partial class EditFieldModel : ObservableObject
    {

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private FieldType type;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private ObservableCollection<EditFieldModel> children = new();

    }
}
