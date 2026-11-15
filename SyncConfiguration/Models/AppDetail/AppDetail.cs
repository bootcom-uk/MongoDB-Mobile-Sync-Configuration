using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SyncConfiguration.Convertors;
using System.Text.Json.Serialization;

namespace SyncConfiguration.Models.AppDetail
{
    public class AppDetail
    {
        [JsonConverter(typeof(ObjectIdConverter))]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonId]
        public ObjectId Id { get; set; }

        public required string BearerToken { get; set; }

        public int Version { get; set; }

        public int FullRefreshIfNoActivityInDays { get; set; }

        public required string AppName { get; set; }

        public string AppDescription { get; set; }

        public required string AppId { get; set; }

        public required List<CollectionMapping> Collections { get; set; }

        public required string Endpoint { get; set; }

        public bool HasInitialSyncComplete { get; set; }

        public AppDetail Clone()
        {
            var newItem = new AppDetail()
            {
                AppDescription = this.AppDescription,
                HasInitialSyncComplete = this.HasInitialSyncComplete,
                AppId = this.AppId,
                AppName = this.AppName,
                FullRefreshIfNoActivityInDays = this.FullRefreshIfNoActivityInDays,
                Version = this.Version,
                BearerToken = this.BearerToken,
                Endpoint = this.Endpoint,
                Id = this.Id,
                Collections = new List<CollectionMapping>()
            };
            foreach(var collection in this.Collections)
            {
                newItem.Collections.Add(new CollectionMapping
                {
                    DatabaseName = collection.DatabaseName,
                    CollectionName = collection.CollectionName,
                    Fields = collection.Fields?.ToList() ?? [],
                    Version = collection.Version
                });
            }
            return newItem;
        }
    }
}
