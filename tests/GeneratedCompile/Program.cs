using Fixtures;
using LiteDB;
using System.ComponentModel;
using System.Text.Json;

var model = new ShoppingList();
var notifications = new List<string>();
((INotifyPropertyChanged)model).PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
model.Title = "Weekly shop";
model.Id = ObjectId.NewObjectId();
var child = new ShoppingList_Items();
var childChanged = false;
child.PropertyChanged += (_, args) => childChanged |= args.PropertyName == "Quantity";
child.Quantity = 3;
child.Checked = true;
var listChanged = false;
model.Items.CollectionChanged += (_, _) => listChanged = true;
model.Items.Add(child);
if (!childChanged || !listChanged) throw new Exception("Nested properties or collections did not notify");
if (!notifications.Contains("Title") || !notifications.Contains("Id")) throw new Exception("Observable properties did not notify");
if (model.Items[0].Prices == null) throw new Exception("Nested list was not initialized");
var document = BsonMapper.Global.ToDocument(model);
if (document["title"].AsString != "Weekly shop" || document["items"].AsArray[0]["quantity"].AsInt32 != 3)
    throw new Exception("Original BSON names did not round-trip");
var restored = BsonMapper.Global.ToObject<ShoppingList>(document);
if (restored.Items[0].Quantity != 3 || restored.Title != model.Title || restored.Id != model.Id)
    throw new Exception("Generated classes could not deserialize");
var json = System.Text.Json.JsonSerializer.Serialize(new ShoppingList_Items { Quantity = 2 });
if (!json.Contains("\"quantity\":2")) throw new Exception("JSON property forwarding failed");
Console.WriteLine("PASS: Combined and separate exported files compile with the real base model and MVVM generator.");
Console.WriteLine("PASS: Root and nested observable properties, initialized lists, BSON round trip, and JSON field names.");
