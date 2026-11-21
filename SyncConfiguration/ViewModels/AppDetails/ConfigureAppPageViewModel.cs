using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extensions;
using MongoDB.Bson;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Services;
using SyncConfiguration.Views.AppDetails;
using Syncfusion.Maui.DataSource;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace SyncConfiguration.ViewModels.AppDetails
{
    public partial class ConfigureAppPageViewModel : ViewModelBase
    {

        [ObservableProperty]
        private AppDetailService appDetailService;

        [ObservableProperty]
        private ObservableCollection<EditDatabaseModel> dataSource;

        /// <summary>Gets or sets validation feedback for collection expiry rules.</summary>
        [ObservableProperty]
        private string expiryStatus;

        private AppDetail _originalAppDetail;

        [ObservableProperty]
        private AppDetail editedAppDetail;

        [ObservableProperty]
        private bool isSyncing;

        [ObservableProperty]
        private string schemaStatus;

        public ConfigureAppPageViewModel(InternalSettingsService internalSettingsService, INavigationService navigationService, AppDetailService appDetailService) : base(internalSettingsService, navigationService)
        {
            AppDetailService = appDetailService;
            IsSyncing = true;
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            try
            {
                SchemaStatus = "Loading collections and fields…";
                await LoadSchema(parameters);
                SchemaStatus = DataSource.Count == 0 ? "No collections were returned by the server." : string.Empty;
            }
            catch (Exception)
            {
                SchemaStatus = "Could not load collections and fields. Check the connection and reopen this app configuration to retry.";
            }
        }

        private async Task LoadSchema(INavigationParameters parameters)
        {
            // Returning from the collection editor must not replace unsaved selections.
            if (DataSource != null && !parameters.ContainsKey("appId"))
                return;
            if(!parameters.TryGetValue<ObjectId>("appId", out var appId)){

                _originalAppDetail = new()
                {
                    FullRefreshIfNoActivityInDays = 30,
                    Collections = new(),
                    AppName = string.Empty,
                    BearerToken = string.Empty,
                    AppDescription = string.Empty,
                    AppId = string.Empty,
                    Endpoint = string.Empty,
                    Version = 0,
                    HasInitialSyncComplete = false
                };

                EditedAppDetail = _originalAppDetail.Clone();

                DataSource = await PrepareEditingDataSource(await AppDetailService.GetSchema(), _originalAppDetail);

                return;
            }

            _originalAppDetail = AppDetailService.AppDetails.FirstOrDefault(app => app.Id == appId);
            _originalAppDetail.Collections = _originalAppDetail.Collections.OrderBy(item => item.CollectionName).ToList();
            EditedAppDetail = _originalAppDetail.Clone();
            EditedAppDetail.Collections = EditedAppDetail.Collections.OrderBy(item => item.CollectionName).ToList();

            DataSource = await PrepareEditingDataSource(await AppDetailService.GetSchema(), _originalAppDetail);

        }

        [RelayCommand]
        async Task ResyncCollection(object arg)
        {
            EditCollectionModel editCollectionModel = (EditCollectionModel) arg;

            IsSyncing = false;
            await AppDetailService.ReSyncCollection(EditedAppDetail, editCollectionModel.DatabaseName, editCollectionModel.CollectionName);
            IsSyncing = true;
        }

        [RelayCommand]
        async Task GenerateModels(EditCollectionModel collection)
        {
            if (collection == null) return;
            await NavigationService.NavigateAsync(nameof(GenerateModelsPage),
                new NavigationParameters { { "collection", collection } });
        }

        [RelayCommand]
        async Task AddNewCollection(object obj)
        {
            var editDatabaseModel = obj as EditDatabaseModel;
            await NavigationService.NavigateAsync(nameof(SaveCollectionPage),
                new NavigationParameters()
                {
                    { "editDatabaseModel", editDatabaseModel }
                });
        }

        /// <summary>
        /// Save the changes to the app detail.
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        async Task Save()
        {
            if (DataSource == null || !string.IsNullOrEmpty(SchemaStatus))
                return;

            ExpiryStatus = string.Empty;
            foreach (var collection in DataSource.SelectMany(database => database.Collections).Where(collection => collection.IsSelected))
            {
                try { MongoDB.Sync.Filtering.SyncFilterRule.Validate(collection.Filter); }
                catch (FormatException ex) { ExpiryStatus = $"{collection.CollectionName}: {ex.Message}"; return; }
                if (string.IsNullOrWhiteSpace(collection.Expiry)) continue;
                var error = new SyncConfiguration.Services.Expiry.ExpiryEditorDocument { Text = collection.Expiry }
                    .Validate(collection.Fields.Where(field => field.IsSelected).Select(field => field.Name));
                if (error is not null) { ExpiryStatus = $"{collection.CollectionName}: {error}"; return; }
            }
            EditedAppDetail.Collections.Clear();

            if(DataSource != null)
            {
                foreach (var item in DataSource)
                {
                    foreach (var collection in item.Collections)
                    {
                        if (collection.IsSelected && collection.Fields.Any(field => field.IsSelected))
                        {
                            EditedAppDetail.Collections.Add(new()
                            {
                                DatabaseName = item.DatabaseName,
                                CollectionName = collection.CollectionName,
                                Expiry = collection.Expiry,
                                Filter = collection.Filter,
                                Fields = collection.Fields.Where(item => item.IsSelected).Select(item => item.Name).ToList(),
                                Version = _originalAppDetail.Collections.FirstOrDefault(mapping => mapping.DatabaseName == item.DatabaseName && mapping.CollectionName == collection.CollectionName)?.Version ?? 0
                            });
                        }
                    }
                }
            }


            var editedContent = JsonSerializer.Serialize(EditedAppDetail);
            var originalContent = JsonSerializer.Serialize(_originalAppDetail);

            if (editedContent == originalContent) return;

            if(string.IsNullOrEmpty(EditedAppDetail.AppId))
            {
                var appIdPrefix = string.Empty;
                for (int i = 0; i < 4; i++)
                {
                    appIdPrefix += (char)Random.Shared.Next(65, 90);
                }
                EditedAppDetail.AppId = $"{appIdPrefix}-{EditedAppDetail.AppName.Substring(0, 4)}";
            }

            await AppDetailService.SaveApp(EditedAppDetail);

            _originalAppDetail = EditedAppDetail.Clone();
            DataSource = await PrepareEditingDataSource(await AppDetailService.GetSchema(), _originalAppDetail);
        }

        /// <summary>
        /// Process the data source for editing.
        /// </summary>
        /// <returns></returns>
        private async Task<ObservableCollection<EditDatabaseModel>> PrepareEditingDataSource(IEnumerable<EditDatabaseModel> fullDatabaseSchema, AppDetail appDetail)
        {

            foreach (var item in fullDatabaseSchema)
            {
                item.Collections = item.Collections.OrderBy(record => record.CollectionName).ToList();
                foreach (var collection in item.Collections)
                {
                    collection.DatabaseName = item.DatabaseName;
                    collection.RestoreSelection([]);
                }
            }

            var newDataSource = new ObservableCollection<EditDatabaseModel>(fullDatabaseSchema);

            appDetail.Collections = appDetail.Collections.OrderBy(item => item.CollectionName).ToList();

            foreach (var appDetailCollection in appDetail.Collections) // Clone list to avoid modification during iteration
            {
                // Find the corresponding database
                var database = newDataSource.FirstOrDefault(record => record.DatabaseName == appDetailCollection.DatabaseName);
                if (database == null)
                {
                    EditedAppDetail.Collections.Remove(appDetailCollection);
                    continue;
                }

                // Find the corresponding collection
                var collection = database.Collections
                    .FirstOrDefault(col => col.CollectionName == appDetailCollection.CollectionName);

                if (collection == null)
                {
                    EditedAppDetail.Collections.Remove(appDetailCollection);
                    continue;
                }

                // Update fields selection
                var parentCollection = EditedAppDetail.Collections
                    .FirstOrDefault(record => record.CollectionName == collection.CollectionName
                                           && record.DatabaseName == appDetailCollection.DatabaseName);

                if (parentCollection != null)
                {
                    collection.RestoreSelection(parentCollection.Fields);
                    collection.Expiry = parentCollection.Expiry;
                    collection.Filter = parentCollection.Filter;
                }
            }

            return newDataSource;
        }

    }
}
