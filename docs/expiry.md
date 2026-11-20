# Cache expiry contract (version 1)

Each entry in `SyncMappings.collections` may contain an optional `expiry` string.
The string contains a JSON Boolean expression. Missing, null and blank values
disable retention. Existing mappings require no migration.

For example, the expression below expires a document 30 days after `completedAt`:

```json
{"$lte":[{"$dateAdd":{"startDate":"$completedAt","unit":"day","amount":30}},"$$NOW"]}
```

The Configuration Tool has a per-collection **Configure expiry** button. Enable
expiry, then use **Designer** or **Expression**. The designer supports nested
All/Any/Not groups, date comparisons, schema date-field pickers, explicit UTC
dates, current time, and nested date offsets. The sample-document preview takes
ordinary JSON ISO date strings or MongoDB Extended JSON dates and an optional
test clock. Apply modifies the draft; Save changes persists the application.
Invalid text is preserved and cannot silently switch into Designer mode.

## Expression semantics

- Boolean operators: `$and`, `$or`, `$not` (array containing one argument).
- Comparisons: `$lt`, `$lte`, `$gt`, `$gte`, `$eq`, `$ne` (two date operands).
- Dates: `$field.path`, `$$NOW`, or `{"$date":"2026-12-31T00:00:00Z"}`.
  Canonical Extended JSON `$date/$numberLong` values are also accepted.
- Offsets: `$dateAdd` or `$dateSubtract` with `startDate`, integer `amount`, and
  `unit`: millisecond, second, minute, hour, day, week, month, quarter or year.
- All arithmetic uses UTC. Months/years use calendar arithmetic, including
  end-of-month clamping. No arbitrary code, timezone expressions or implicit
  conversion of source string fields is supported. Source/device fields must be
  BSON dates. Synchronize the referenced field or its containing object.
- Rule limits: 16 KiB of JSON, 12 expression levels, 32 children per Boolean group,
  and offset amounts from -100000 to 100000. Overflow retains the document.
- The result must be Boolean. Malformed/unsupported rules are logged and ignored.
  Missing/wrongly typed document fields retain the document, even in a Boolean
  branch that could otherwise short-circuit. Diagnostics are bounded per rule.

The interpreter in `MongoDB.Sync.Models/Expiry` is intentionally independent of
MongoDB and LiteDB. Its source is mirrored byte-for-byte into the Apps models
project and Configuration Tool's `Services/Expiry`. Keep the two interpreter
files identical when evolving the contract; there are no cross-repository build
references or new BootCom NuGet dependencies.

## Device behaviour

Cleanup runs on cache initialization, startup/resume through sync startup, after
downloads, and once per minute while the cache service is alive. Each sweep
captures one UTC time and yields the queue/write lock between 500-document pages.
Incoming API and live data are checked before persistence. Offline sweeps use
the saved mapping and the device UTC clock; an inaccurate device clock can shift
expiry timing. Suspended mobile apps catch up when execution resumes.

Expiry deletes only LiteDB cache rows and sends UI notifications. It never calls
the local user-delete method, enqueues a source deletion, or clears pending
uploads. Records with pending edits are retained until the upload queue clears.
Live updates can restore an evicted record if it becomes eligible again.

## API and AppServices behaviour

API sync pages replace expired bodies with cache-only removal markers. Actual
source-deletion tombstones are preserved. New clients send `UseCursor=true`, a
fixed `LastSyncDate` and the previous response's `LastSyncedId`. The API combines
those bounds and does not use offset pagination for cursor requests. The first
page also returns a fixed `SyncThrough` upper timestamp; subsequent pages resend
it, and the client commits it only after the entire download succeeds. Restores
and writes after that boundary remain eligible for the next pass.

Changing/removing expiry increments collection and mapping versions and rebuilds
the AppServices cache from the source so previously evicted data can return.
Both completion event names are handled by updated Apps. Administrative saves
must go through the API/Configuration Tool for versioning and rebuilds; editing
MongoDB directly does not perform that lifecycle.

`SyncExpiryWorker` periodically scans enabled, initially synchronized mappings.
It reads source collections, restores eligible missing cache rows with
`$setOnInsert`, and deletes expired AppServices rows only if their observed values
still match. It never writes to source collections and does not remove source
deletion tombstones. Restores/removals notify connected devices through SignalR.
Reconciliation also supports rules whose result can change back to false.

The worker uses 250-document cursor batches, but each pass scans the source and
cache collections for enabled rules. This is not an indexed TTL implementation;
measure database load for large collections. Multiple worker instances are
idempotent but repeat reads. Configure one API service to own maintenance:

```json
{"SyncExpiry":{"MaintenanceEnabled":true,"MaintenanceIntervalSeconds":60}}
```

Defaults are enabled and 60 seconds (interval clamped to 30–86400 seconds). Set
`MaintenanceEnabled=false` on other API services, or while staging the rollout.
No live database jobs, indexes or production settings are changed by this patch.

## Rollout and verification

Deploy Apps with cursor support before enabling expiry cleanup in the API.
New Apps tolerate older servers that still return legacy pagination responses.
Older Apps ignore expiry and use offset pagination, so they must be upgraded
before rules/physical cleanup are enabled. Then deploy the API and Configuration
Tool, validate a sample rule, and enable it for a collection.

Targeted checks:

```text
dotnet run --project Apis/Tests/SyncExpiry.Tests/SyncExpiry.Tests.csproj
dotnet run --project Tests/SyncExpiry.Tests/SyncExpiry.Tests.csproj       (Apps)
dotnet run --project Tests/SyncPerformance.Tests/SyncPerformance.Tests.csproj (Apps)
dotnet run --project tests/ExpiryChecks/ExpiryChecks.csproj               (Configuration Tool)
```

API tests exercise the real service against observable driver doubles; device
tests use a temporary LiteDB database. No live MongoDB or deployed Atlas triggers
are exercised. External source-to-AppServices ingestion may briefly recreate an
expired cache row; API/device checks suppress it, and maintenance removes it.
