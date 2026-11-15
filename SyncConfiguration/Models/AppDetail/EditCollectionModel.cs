using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Sync.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;

namespace SyncConfiguration.Models.AppDetail
{
    public partial class EditCollectionModel : ObservableObject
    {
        [ObservableProperty]
        private string collectionName;

        [ObservableProperty]
        private string databaseName;    

        // Changed from List<T> to ObservableCollection<T> so UI updates instantly
        [ObservableProperty]
        private ObservableCollection<EditFieldModel> fields = new();

        [ObservableProperty]
        private bool isSelected;

        private bool updatingSelection;

        partial void OnIsSelectedChanged(bool value)
        {
            if (updatingSelection) return;
            updatingSelection = true;
            foreach (var field in Fields)
                field.IsSelected = value;
            updatingSelection = false;
        }

        public void RestoreSelection(IEnumerable<string> selectedFields)
        {
            var selected = new HashSet<string>(selectedFields ?? []);
            // A sampled schema can omit saved fields, especially for empty collections.
            foreach (var name in selected.Except(Fields.Select(field => field.Name)).ToList())
                Fields.Add(new EditFieldModel { Name = name, Type = new FieldType { Type = "Unknown" } });
            updatingSelection = true;
            IsSelected = selected.Count > 0;
            foreach (var field in Fields)
                field.IsSelected = selected.Contains(field.Name);
            updatingSelection = false;
        }

        // New field entry properties
        [ObservableProperty]
        private string newFieldName;

        [ObservableProperty]
        private string newFieldType;

        public ObservableCollection<string> AvailableFieldTypes { get; } =
            new ObservableCollection<string> { "String", "Number", "Boolean", "Date", "ObjectId" };

        // Command to add a new field
        public IRelayCommand AddFieldCommand { get; }

        public EditCollectionModel()
        {
            AddFieldCommand = new RelayCommand(AddField);
            Fields.CollectionChanged += FieldsChanged;
        }

        partial void OnFieldsChanging(ObservableCollection<EditFieldModel> value)
        {
            Fields.CollectionChanged -= FieldsChanged;
            foreach (var field in Fields) field.PropertyChanged -= FieldChanged;
        }

        partial void OnFieldsChanged(ObservableCollection<EditFieldModel> value)
        {
            value.CollectionChanged += FieldsChanged;
            foreach (var field in value) field.PropertyChanged += FieldChanged;
        }

        private void FieldsChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.OldItems != null)
                foreach (EditFieldModel field in args.OldItems) field.PropertyChanged -= FieldChanged;
            if (args.NewItems != null)
                foreach (EditFieldModel field in args.NewItems) field.PropertyChanged += FieldChanged;
        }

        private void FieldChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!updatingSelection && args.PropertyName == nameof(EditFieldModel.IsSelected))
            {
                // Update the checkbox without applying select-all to the other fields.
                updatingSelection = true;
                IsSelected = Fields.Any(field => field.IsSelected);
                updatingSelection = false;
            }
        }

        private void AddField()
        {
            if (string.IsNullOrWhiteSpace(NewFieldName) || string.IsNullOrWhiteSpace(NewFieldType))
                return;

            // Keep a newly added field in an enabled collection without changing existing choices.
            updatingSelection = true;
            IsSelected = true;
            updatingSelection = false;

            Fields.Add(new EditFieldModel
            {
                Name = NewFieldName,
                Type = new FieldType { Type = NewFieldType },
                IsSelected = true
            });

            // Reset entry fields
            NewFieldName = string.Empty;
            NewFieldType = null;
        }
    }
}
