using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncConfiguration.Models.AppDetail
{
    public partial class FieldTypesLookup : ObservableObject
    {

        [ObservableProperty]
        private ObservableCollection <String> fieldTypes = [];

        public FieldTypesLookup()
        {
            Enum.GetValues(typeof(FieldType)).Cast<FieldType>().ToList().ForEach(record => FieldTypes.Add(record.ToString()));
        }

        public enum FieldType
        {
            ObjectId,
            Text,
            Int,
            Long,
            Boolean,
            Date,
            Array,
            GeoPoint
        }

    }
}
