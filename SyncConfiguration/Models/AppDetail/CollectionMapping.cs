namespace SyncConfiguration.Models.AppDetail
{
    public class CollectionMapping
    {

        public required string CollectionName { get; set; }

        public required string DatabaseName { get; set; }

        public List<string> Fields { get; set; }

        public int Version { get; set; }

    }
}
