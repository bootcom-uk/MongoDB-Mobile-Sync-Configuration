# Generate local-cache models

Open an application configuration, expand a collection, and choose **Generate C# models**.
The generator uses all available fields by default. Enable **Selected sync fields only** to use the current checkbox selection, including unsaved choices.

Set the root class name and destination namespace. Under **Integration settings**, you can change the namespace containing your existing `BaseLocalCacheModel` and the local collection mapping. The defaults match this project's companion sync library: `LiteDB.ObjectId` and `[CollectionName("database_collection")]`, replacing hyphens with underscores.

Choose **Generate** after changing settings or the sample. Select a class in the dropdown to inspect it:

- **Copy class** copies one complete source file, including its namespace and imports.
- **Copy all classes** copies a single compilable source file containing the root and all nested classes.
- **Export .cs files (ZIP)** opens a save dialog for one file per class plus integration/review notes.

The root inherits `BaseLocalCacheModel`, so its `Id` is not redeclared. Nested objects inherit `ObservableObject`. For example, the `items` object array in `ShoppingList` produces `ShoppingList_Items`, and an object array named `prices` inside it produces `ShoppingList_Items_Prices`. Lists use initialized `ObservableCollection<T>`. Fields use `[ObservableProperty]`, with source field names forwarded onto the generated properties using `BsonField` and `JsonPropertyName`.

## Missing schema information

The current schema endpoint can return `Array<Object>` without the child fields. The generator creates the named class but clearly marks it incomplete. Expand **Paste sample JSON**, paste a representative document or an array of documents, then regenerate. Ordinary JSON and MongoDB Extended JSON wrappers such as `$oid`, `$date`, and `$numberDecimal` are supported. Sample shapes are merged across array elements and documents, and absent/null fields become nullable when inferred from samples.

Samples are processed locally; their values are neither exported nor sent to the server. Samples do not alter the synchronization configuration. Only names and types are used. Existing schema scalar types take precedence over sample guesses, and samples add missing nested shapes. Empty arrays, null-only fields and mixed values produce explicit review notes rather than invented types.

## Integration

Keep your existing base class and reference CommunityToolkit.Mvvm, LiteDB and the companion MongoDB sync packages. Review namespaces, field types and collection mapping before using the files. ObjectId fields are emitted as identifiers; schema alone cannot identify references to other application models.

Names that are invalid C# identifiers or collide with generated members are normalized and reported. Mapping attributes preserve original BSON/JSON names for standard serializers. Custom sync resolvers that match CLR property names directly may need corresponding mappings for renamed fields.

The schema is sampled, so it is not a substitute for checking the collection's full data contract. Fill missing nested shapes before using incomplete generated classes.
