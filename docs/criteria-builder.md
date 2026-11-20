# Reusable criteria builder

`Controls/Criteria/CriteriaBuilder` is a MAUI ContentView. Bind `Text` two-way and provide `CriteriaBuilderOptions`. Consumers supply fields, typed operators, functions, allowed groups, a default condition factory and an `ICriteriaTextAdapter`. Optional validation belongs to the consumer. The control does not save data or depend on MongoDB.

The generic tree and `CriteriaDocument` live in `Criteria`. Supported operand types are date/time, string, number, Boolean and fixed choices. Functions declare typed parameters and readable formatting. All/Any/Not groups preserve child conditions when regrouped. Invalid text remains editable and cannot overwrite the last valid visual tree.

`MongoExpiryCriteriaAdapter` is the first consumer. It translates the tree to the existing MongoDB expiry JSON contract. The expiry page owns enable/disable, selected-field validation, sample evaluation and Apply/Cancel. Dates use UTC; applying only changes the collection draft.

The designer uses compact clickable tokens and searchable contextual editors. Designer/JSON switching preserves the original text until a visual edit is committed. Unsupported expressions remain in text mode with an error.

Run targeted checks with `dotnet run --project tests/ExpiryChecks/ExpiryChecks.csproj`. They include an unrelated business-rule adapter to verify that the model is independent of expiry and MongoDB.

For offline Windows UI inspection, build Debug with `-p:WindowsPackageType=None`, set `SYNC_CRITERIA_PREVIEW=1` for that process and launch SyncConfiguration.exe. The debug-only harness bypasses normal startup and opens a synthetic collection; it does not connect to services or persist data. Close the preview before rebuilding. Normal startup is unchanged when the variable is absent.
