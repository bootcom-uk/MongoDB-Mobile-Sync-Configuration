namespace SyncConfiguration.Models.AppDetail
{
    public class CollectionMapping
    {

        public required string CollectionName { get; set; }

        public required string DatabaseName { get; set; }

        public List<string> Fields { get; set; }

        /// <summary>Gets or sets the optional cache-expiry expression.</summary>
        public string? Expiry { get; set; }

        /// <summary>Gets or sets the server-enforced visibility filter.</summary>
        public string? Filter { get; set; }

        public int Version { get; set; }

    }
}
