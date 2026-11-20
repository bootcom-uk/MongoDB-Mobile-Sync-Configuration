using CommunityToolkit.Mvvm.ComponentModel;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncConfiguration.ViewModels.AppDetails
{
    public partial class SaveCollectionPageViewModel : ViewModelBase
    {

        public SaveCollectionPageViewModel(InternalSettingsService internalSettingsService, INavigationService navigationService) : base(internalSettingsService, navigationService)
        {
        }

        public enum FieldType
        {
            String,
            Int,
            Bool,
            Double,
            Decimal,
            Date,
            Object,
            Array,
            Binary,
            ObjectId,
            Uuid,
            Null
        }

        public List<FieldType> AvailableTypes { get; } =
        Enum.GetValues(typeof(FieldType)).Cast<FieldType>().ToList();

        private EditDatabaseModel _dataSource;

       

        [ObservableProperty]
        private string collectionName;

        [ObservableProperty]
        private ObservableCollection<EditFieldModel> fields = [];

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            parameters.TryGetValue<EditDatabaseModel>("editDatabaseModel", out var editDatabaseModel);

            _dataSource = editDatabaseModel;
        }

    }
}
