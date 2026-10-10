# Architecture

The app is a static Blazor WebAssembly PWA that keeps all data on the phone and syncs it through a small versioned document store behind an ASP.NET Core API. All domain logic lives in the client (Core). The server only authenticates people, scopes data to their household, stamps versions and hands back changes.

```
iPhone (x2)                                   Azure, free tiers
┌───────────────────────────────┐             ┌────────────────────────────────┐
│ MealPlanner.App (Blazor WASM) │   HTTPS     │ Static Web Apps (Free)         │
│  Pages → Services → AppState  │◄────────────│  serves the PWA files          │
│        │            │         │             └────────────────────────────────┘
│        ▼            ▼         │             ┌────────────────────────────────┐
│   MealPlanner.Core  IStore    │  POST /sync │ App Service F1 (Linux)         │
│   (logic)      (localStorage) │────────────►│  MealPlanner.Api (minimal API) │
│                SyncService ───┼─ bearer JWT │  EF Core → SQLite /home/data   │
└───────────────────────────────┘             └────────────────────────────────┘
```

## Solution layout

```
MealPlanner.sln
Directory.Build.props            net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
src/
  MealPlanner.Core/
    Entities/                    Entity, Market, Ingredient, Meal, MealIngredient, PlannedMeal,
                                 ShoppingList, ShoppingItem, ItemSource
    Units.cs  Weeks.cs  Ids.cs  QuantityFormat.cs  Validation.cs
    Shopping/                    ShoppingListBuilder.cs, ShoppingListLayout.cs
    Sync/                        SyncContracts.cs (request/response DTOs), EntityTypes.cs
    Backup/                      BackupFile.cs
  MealPlanner.App/
    Pages/                       Welcome (includes Sign in), CreateHousehold, JoinHousehold, Meals,
                                 MealEdit, Ingredients, Plan, Shopping, Settings
    Components/                  shared UI (docs/ui.md, Components), Icons/
    Layout/                      AppShell.razor (tab bar, banners), NavGuard
    Services/                    AppState, MealService, IngredientService, MarketService,
                                 PlanService, ShoppingService, BackupService, SyncService,
                                 AuthService, PreferencesService, Clock
    Storage/                     IStore, LocalStorageStore, AppData
    wwwroot/                     index.html, manifest.webmanifest, service-worker*.js,
                                 css/tokens.css, css/app.css, fonts/, icons/, js/interop.js,
                                 lib/sortable/Sortable.min.js, staticwebapp.config.json
  MealPlanner.Api/
    Program.cs  Endpoints/ (Auth, Household, Sync, Health)  Data/ (AppDb, migrations)
    Auth/ (PasskeyService, TokenService)  Options/
tests/
  MealPlanner.Core.Tests/
  MealPlanner.Api.Tests/
.github/workflows/ci.yml  deploy.yml
docs/
```

## Packages

| Project | Packages |
| --- | --- |
| App | Microsoft.AspNetCore.Components.WebAssembly (from the template), Blazored.LocalStorage |
| App (vendored files, not npm) | SortableJS `Sortable.min.js` in `wwwroot/lib/sortable/`; Figtree `.woff2` (400–800, SIL OFL) in `wwwroot/fonts/` |
| Api | Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design, Microsoft.AspNetCore.Authentication.JwtBearer, Fido2 (fido2-net-lib) |
| Tests | xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing (Api.Tests) |

The app is offline, so fonts and scripts are vendored, never loaded from a CDN.

## Core data model

```csharp
public abstract record Entity
{
    public required Guid Id { get; init; }
    public DateTime UpdatedAt { get; init; }      // UTC, set by the client service on every change
    public bool IsDeleted { get; init; }          // soft delete; syncs like any change
    public long Version { get; init; }            // set by the server; 0 = never synced
}

public sealed record Market : Entity
{
    public required string Name { get; init; }
    public int SortOrder { get; init; }                        // order in Settings and in "By market"
    public IReadOnlyList<Guid> ItemOrder { get; init; } = [];  // ingredient ids, manual order in this market
}

public sealed record Ingredient : Entity
{
    public required string Name { get; init; }
    public string Unit { get; init; } = "";        // one of Units.All; "" = no unit
    public Guid? DefaultMarketId { get; init; }    // null = Anywhere
    public bool IsStaple { get; init; }
}

public sealed record MealIngredient(Guid IngredientId, decimal? Quantity);   // null = "some"

public sealed record Meal : Entity
{
    public required string Name { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<MealIngredient> Ingredients { get; init; } = [];     // in the order added
}

public sealed record PlannedMeal : Entity          // one entry; a meal planned twice = two entries
{
    public required DateOnly WeekStart { get; init; }   // always a Monday
    public required Guid MealId { get; init; }
    public required DateTime AddedAt { get; init; }     // UTC; orders the "By meal" groups
}

public sealed record ShoppingList : Entity         // Id = Ids.ForList(WeekStart)
{
    public required DateOnly WeekStart { get; init; }
    public required DateTime GeneratedAt { get; init; }
}

public sealed record ItemSource(Guid MealId, string MealName, int Times, decimal? Quantity);
// Quantity = meal quantity × Times, or null when the meal row has no quantity

public sealed record ShoppingItem : Entity         // ingredient item: Id = Ids.ForItem(WeekStart, IngredientId)
{                                                  // manual item: Id = Guid.NewGuid()
    public required DateOnly WeekStart { get; init; }
    public Guid? IngredientId { get; init; }       // null = manual item
    public required string Name { get; init; }
    public string Unit { get; init; } = "";
    public decimal? Quantity { get; init; }        // sum of known source quantities; null = none known
    public bool HasUnspecified { get; init; }      // at least one source had no quantity
    public IReadOnlyList<ItemSource> Sources { get; init; } = [];   // empty for manual items
    public Guid? MarketId { get; init; }           // null = Anywhere
    public bool MarketOverridden { get; init; }    // moved for this week only
    public bool IsStaple { get; init; }
    public DateTime? CheckedAt { get; init; }      // null = not bought
}
```

Lists are `IReadOnlyList<T>`; services replace them whole (`meal with { Ingredients = [..meal.Ingredients, row] }`).

### Helpers in Core

- `Units.All`: `["", "g", "kg", "ml", "l", "pc", "clove", "tsp", "tbsp", "cup", "tin", "pack", "bunch", "slice"]`.
- `Weeks.Name(week, localNow)` ("This week", "Next week", "Last week", "Week of Oct 26"), `Weeks.RangeLabel(week)` ("Mon Oct 12 – Sun Oct 18"), and `Weeks.PlanMin/PlanMax(localNow)`: the Plan range, 2 weeks before to 4 weeks after the current week (not the active week). Labels are English and culture-independent.
- `Weeks.StartOf(DateOnly d)`: the Monday on or before `d`. `Weeks.Active(DateTime localNow)`: `StartOf(today)` Monday to Thursday, the next Monday Friday to Sunday. `Weeks.RetentionCutoff(localNow)`: `StartOf(today).AddDays(-14)`.
- `Ids.ForList(DateOnly weekStart)` and `Ids.ForItem(DateOnly weekStart, Guid ingredientId)`: name-based UUIDv5 (RFC 9562, SHA-1). Use a fixed namespace GUID defined once in `Ids.cs`, and the names `"list:{yyyy-MM-dd}"` and `"item:{yyyy-MM-dd}:{ingredientId:D}"`. Because both phones compute the same ids, lists created concurrently converge to one list.
- `QuantityFormat.Format(decimal? q, bool hasUnspecified, string unit)`:
  - null → `""`
  - otherwise the number (invariant culture, at most 2 decimals, trailing zeros dropped), then `" " + unit` when the unit is not empty, then `" +"` when `hasUnspecified`
  - examples: `(500,false,"g")` → `"500 g"`, `(1.5,false,"pc")` → `"1.5 pc"`, `(2,true,"pc")` → `"2 pc +"`, `(null,true,"")` → `""`
- `QuantityFormat.TryParse(string input, out decimal? value)`: accepts `,` or `.`, enforces the rules in spec › Validation.
- `Validation`: one static method per rule in spec › Validation, returning an error message or null. Name rules take the existing entities and the id being edited (`MealName(name, meals, editingId)`), so an entity never clashes with itself. Invite codes are normalized with `NormalizeInviteCode` and must be 8 Crockford base32 characters.
- `CoreJson.Options`: the shared `JsonSerializerOptions` (`JsonSerializerDefaults.Web`) for storage, sync and backup.
- `EntityTypes`: the six type names, `Serialize(entity)` and `Deserialize(type, json)` (null for an unknown type or bad JSON).
- `BackupFile`: `SchemaVersion` (1), `ExportedAt`, and one list per entity type (`Markets`, `Ingredients`, `Meals`, `PlannedMeals`, `ShoppingLists`, `ShoppingItems`). `Create(...)` drops deleted entities; `Serialize()`; `Deserialize(json)` returns null for unreadable files or an unknown schema version; `FileName(date)`.

### ShoppingListBuilder

`Build(DateOnly week, IEnumerable<PlannedMeal>, IReadOnlyDictionary<Guid, Meal>, IReadOnlyDictionary<Guid, Ingredient>) → IReadOnlyList<ShoppingItem>`

1. Take the non-deleted entries for `week`. Group them by `MealId` and count `Times`. Order the groups by their earliest `AddedAt`. Skip meals that are missing or deleted.
2. For each meal, in that order, and each `MealIngredient` in the meal's order (skip missing or deleted ingredients), add an `ItemSource(meal.Id, meal.Name, times, row.Quantity * times)` to that ingredient's bucket.
3. For each bucket, create a `ShoppingItem`:
   - `Id = Ids.ForItem(week, ingredientId)`
   - `Name`, `Unit` and `IsStaple` from the ingredient; `MarketId = ingredient.DefaultMarketId`
   - `Quantity` = sum of the non-null source quantities, or null if all are null
   - `HasUnspecified` = any source quantity is null
   - `CheckedAt = null`, `MarketOverridden = false`
4. Return the items in first-seen order.

`IsOutOfDate(existingItems, builtItems) → bool`: compare only ingredient items (`IngredientId != null`) that are not deleted. Returns true if the sets of ingredient ids differ, or if any matching pair differs in `Name`, `Unit`, `Quantity`, `HasUnspecified`, `IsStaple`, `Sources` (sequence-equal), or in `MarketId` when the existing item is not `MarketOverridden`.

`Merge(existingItems, builtItems, DateTime now) → IReadOnlyList<ShoppingItem>` (the changed entities to save):

- For a built item with a live existing match: take the built values, but keep the existing `Id`, `CheckedAt`, `MarketOverridden`, and also `MarketId` when overridden. If built `Quantity ?? 0` is greater than existing `Quantity ?? 0`, set `CheckedAt = null`.
- For a built item with no match, or whose match is deleted: add it as new (`IsDeleted = false`, unticked).
- For an existing live ingredient item with no built match: mark it `IsDeleted = true`.
- Never return manual items.
- Return only the items that actually changed. Each gets `UpdatedAt = now`.

### ShoppingListLayout

A pure function from (list items, markets, meals in plan order, view) to groups of display rows. Rules are in spec › Shopping list › Views. Summary:

- **By market:**
  - Anywhere first (`MarketId` null or pointing to a deleted market), then markets by `SortOrder`; empty groups dropped.
  - Within a market: unticked items in `ItemOrder` position, then unticked items not in `ItemOrder` (manual items included) alphabetically, then ticked items by `CheckedAt` ascending. Anywhere: unticked alphabetically, then ticked.
- **By meal:**
  - One group per meal, in plan order. A row per source: the item plus that source's quantity and "Also in" names.
  - Unticked rows follow the meal's ingredient order; ticked rows sink, ordered by `CheckedAt`.
  - Manual items in an "Extras" group, alphabetical with ticked ones last.
- **Both views:** staples are removed from the groups and listed in a final "Check you have" group, alphabetical with ticked ones last.
- `Progress(items)`: `(bought, total)` over live, non-staple items.
- Output: `ShoppingGroup(Kind, Id, Title, Times, Rows)` with `Kind` one of `Anywhere`, `Market`, `Meal`, `Extras`, `CheckYouHave`, and `ShoppingRow(Item, Quantity, HasUnspecified, MealNames)`. In a meal group a row's quantity is that source's amount and `MealNames` are the other meals ("Also in"); elsewhere it is the item total and every meal that needs it.
- By meal: meal groups come from the items' sources. A meal in the sources but no longer in the plan (list out of date) is grouped after the planned meals. Alphabetical order is case-insensitive.

## Client (App)

### Storage

- `IStore` has `Task<AppData?> LoadAsync()` and `Task SaveAsync(AppData data)`. `LocalStorageStore` keeps the whole `AppData` as one JSON value under the key `mealplanner.data`, using Blazored.LocalStorage. It is a few hundred KB at most.
- `AppData`:
  - `SchemaVersion` (start at 1)
  - `Dictionary<Guid, Entity>` per entity type
  - `Outbox`: `Dictionary<Guid, (string Type, long Seq)>`
  - `NextOutboxSeq`
  - `LastVersion` (the last server version seen)
- Auth tokens live under a separate key, `mealplanner.auth`. Preferences live under `mealplanner.prefs`.
- Serialize with `System.Text.Json`. Each entity type is stored in its own dictionary, so no polymorphic serialization is needed.

### State and services

- `AppState` (singleton): in-memory `AppData` plus derived lookups. Raises `event Action? OnChange`. Pages subscribe in `OnInitialized` and unsubscribe in `Dispose`.
- Each mutating service method:
  1. validates
  2. builds the new record(s) with `UpdatedAt = Clock.UtcNow`
  3. replaces them in `AppState`
  4. adds `(type, NextOutboxSeq++)` to `Outbox` for each id
  5. saves with `IStore.SaveAsync`
  6. raises `OnChange`
  7. calls `SyncService.RequestSync()`
- Steps 3–7 live in one place, `EntityWriter.SaveAsync(entities)`. Services validate and build the records, then make one `SaveAsync` call per operation.
- Services request syncs through `ISyncTrigger`. While `Features:RequireAuth` is false, `NoSyncTrigger` is registered and changes simply stay in the outbox; from M6 `SyncService` implements it.
- Editors hand services plain drafts: `MealDraft(Id?, Name, Notes, Ingredients)` and `IngredientDraft(Id?, Name, Unit, DefaultMarketId, IsStaple)`. Each service also exposes `Validate(draft)`/`ValidateName(...)` so Save buttons can stay disabled until valid.

| Service | Methods |
| --- | --- |
| MealService | `SaveMeal(MealDraft)`, `DeleteMeal(id)` (also removes current and future `PlannedMeal`s) |
| IngredientService | `SaveIngredient(IngredientDraft)`, `CanDelete(id)`, `DeleteIngredient(id)`, `IsUsed(id)`, `UsedBy(id)` |
| MarketService | `Add(name)`, `Rename(id, name)`, `Delete(id)` (clears `DefaultMarketId` on its ingredients), `Reorder(ids)`, `SetItemOrder(marketId, ingredientIds)` |
| PlanService | `Add(week, mealId)`, `RemoveOne(week, mealId)` (newest entry first), `CountFor(week, mealId)`, `IsReadOnly(week)` |
| ShoppingService | `GetState(week)` (None / UpToDate / OutOfDate), `CreateOrUpdate(week)`, `Toggle(itemId)`, `MoveToMarket(itemId, marketId?)`, `AddManual(...)`, `EditManual(...)`, `DeleteManual(id)` |
| BackupService | `Export()`, `Preview(file)`, `Import(file)` |
| PreferencesService | `ShoppingView` |
| Clock | `UtcNow`, `LocalNow` (wrap `DateTime` so tests can fix time) |

Reorder in By market saves `ItemOrder` as the dragged ingredient ids followed by the market's earlier `ItemOrder` ids that aren't on this week's list, so items from other weeks keep their relative order (`ShoppingService.SaveMarketOrder`).

On startup and after each sync, `PlanService` prunes old weeks. Every `PlannedMeal`, `ShoppingList` and `ShoppingItem` with `WeekStart < Weeks.RetentionCutoff(now)` is soft-deleted through the normal path, so the deletion syncs. Both phones may do this; it is idempotent.

### SyncService

- **Triggers:**
  - app start
  - `visibilitychange` to visible
  - `online` event
  - `RequestSync()` (debounced 1 s)
  - a timer while the document is visible: every 10 s on the Shopping page, every 30 s on other pages
- One sync runs at a time. If a sync is requested while one is running, one more runs after it.
- **Algorithm:**
  1. Snapshot the outbox: ids and their seqs. Build `changes` from the current local entities for those ids.
  2. `POST /api/sync { since: LastVersion, changes }` with the bearer token. Timeout 45 s, because the first request after the server sleeps is slow.
  3. Remove each pushed id from `Outbox` if its seq is unchanged since the snapshot.
  4. Apply the response:
     - Skip an incoming entity whose id is still in `Outbox`; the local edit is newer and goes out next time.
     - Otherwise, replace the local entity (including `Version`).
     - If `full` is true, first drop every local entity that is not in the response and not in `Outbox`.
  5. Set `LastVersion = response.ServerVersion`. Save, raise `OnChange`, run pruning.
- **Errors:**
  - 401: try a refresh once, then retry the sync. If the refresh fails, go to `/welcome` with "Please sign in again", keeping local data.
  - Network failure or 5xx: retry with backoff (2 s, 5 s, 15 s, 30 s, then every 60 s).
  - Offline banner: shown when `navigator.onLine` is false, or when the last attempt failed and the last success was more than 60 s ago.
- Steps 1 and 4 are pure functions in Core (`SyncMerge.BuildChanges`, `SyncMerge.Apply`) so they are unit-tested; `SyncService` does the I/O. Incoming entities go straight into `AppState` (never through `EntityWriter`), so they don't re-enter the outbox.
- `full` handling covers a phone that was away long enough for the server to purge tombstones it never saw.

### PWA

- Start from `dotnet new blazorwasm --pwa`. Keep its service worker, which caches the app shell and `_framework`, so the app runs offline.
- **Update flow:** `js/interop.js` registers the service worker and listens for `updatefound`. When the new worker is `installed` and a controller exists, it calls .NET to show the "New version available – Reload" banner. Reload sends `SKIP_WAITING` to the waiting worker and reloads on `controllerchange`. Add a `message` handler for `SKIP_WAITING` in `service-worker.published.js`.
- **`index.html`:**
  - `<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">`
  - `apple-mobile-web-app-capable=yes`
  - `apple-mobile-web-app-status-bar-style=default`
  - `apple-touch-icon` 180 px
  - `theme-color` with media queries for light (`#F5F4EF`) and dark (`#121412`)
- **Icons:** `icons/source.svg` (repo root) is the single source. `icons/build.sh` renders it with macOS built-ins (`qlmanage`, `sips`) into `wwwroot/icons/`: `icon-192.png`, `icon-512.png`, `icon-maskable-512.png`, `apple-touch-icon.png` (180) and `favicon-32.png`. Rerun it after editing the SVG.
- **Offline cache:** `service-worker.published.js` caches `.woff2` as well as the template's types, so Figtree works offline.
- **`manifest.webmanifest`:** `name` "Meal Planner", `short_name` "Meals", `display` standalone, `start_url` "./", `background_color` `#F5F4EF`, `theme_color` `#2E7A4B`, icons 192 and 512 (plus maskable 512).
- **`staticwebapp.config.json`:**
  - `navigationFallback.rewrite = "/index.html"`, excluding `/_framework/*`, `/css/*`, `/js/*`, `/fonts/*`, `/icons/*`, `/lib/*`, `*.{json,webmanifest,js,css,woff2,png,svg,wasm,dll,dat,blat}`
  - `Cache-Control: no-cache` on `/service-worker.js` and `/index.html`
- `ApiBaseUrl` comes from `wwwroot/appsettings.json` (dev: `https://localhost:7001`) and `wwwroot/appsettings.Production.json` (the App Service URL).

## Server (Api)

### Tables (EF Core, SQLite)

| Table | Columns | Keys / indexes |
| --- | --- | --- |
| Households | Id, Name, Version (long), PurgedThroughVersion (long), LastPurgeAt, CreatedAt | PK Id |
| Users | Id, HouseholdId, DisplayName, CreatedAt | PK Id, IX HouseholdId |
| Passkeys | CredentialId (blob), UserId, PublicKey (blob), SignCount, UserHandle (blob), CreatedAt, LastUsedAt | PK CredentialId, IX UserId |
| RefreshTokens | TokenHash (SHA-256 hex), UserId, CreatedAt, ExpiresAt, RevokedAt | PK TokenHash |
| Invites | Code, HouseholdId, CreatedBy, CreatedAt, ExpiresAt, UsedAt, UsedBy | PK Code |
| Entities | HouseholdId, Id, Type, Json, IsDeleted, Version, UpdatedAt, UpdatedBy | PK (HouseholdId, Id), IX (HouseholdId, Version) |

The server treats entity `Json` as opaque, apart from validation. All domain rules run on the clients.

### Endpoints

All endpoints are under `/api` and use JSON. Everything except `auth/*` and `health` needs `Authorization: Bearer <access token>`.

| Method and path | Body → response | Notes |
| --- | --- | --- |
| `GET health` | → `200 "ok"` | The client calls it on app start to wake the server. |
| `POST auth/register/begin` | `{ displayName, inviteCode? }` → `{ flowId, options }` | `options` is the WebAuthn `PublicKeyCredentialCreationOptions` (resident key required, user verification required). An invalid or expired invite returns 400 `{ error: "invite_invalid" }`. |
| `POST auth/register/complete` | `{ flowId, attestation }` → `TokenPair` | Creates the user and passkey. With no invite, it also creates a household named "{displayName}'s household". With an invite, it joins that household (409 `household_full` at 4 members) and marks the invite used. |
| `POST auth/login/begin` | `{}` → `{ flowId, options }` | Discoverable credential: empty `allowCredentials`. |
| `POST auth/login/complete` | `{ flowId, assertion }` → `TokenPair` | Verifies the assertion, updates `SignCount` and `LastUsedAt`. |
| `POST auth/refresh` | `{ refreshToken }` → `TokenPair` | Rotates: revokes the old token and issues a new one. |
| `POST auth/logout` | `{ refreshToken }` → 204 | Revokes the token. |
| `GET me` | → `{ userId, displayName, household: { id, name }, members: [{ userId, displayName }] }` | |
| `POST household/invites` | → `{ code, expiresAt }` | 8 characters from Crockford base32, valid 48 h. |
| `POST sync` | `SyncRequest` → `SyncResponse` | See the sync protocol below. |

Request and response bodies are records in `Core/Auth/AuthContracts.cs`, shared by App and Api. WebAuthn options, attestations and assertions travel as raw JSON (`JsonElement`) with base64url binary fields. Error bodies are `{ error }` with codes from `AuthErrors` (`invite_invalid`, `household_full`, `invalid_request`, `passkey_failed`, `flow_expired`, `invalid_token`).

`TokenPair` is `{ accessToken, accessTokenExpiresAt, refreshToken }`:

- **Access token:** a JWT signed with HMAC-SHA256. Lifetime 15 min. Claims: `sub` = userId, `hid` = householdId.
- **Refresh token:** 32 random bytes, base64url. Lifetime 180 days. The server stores only its SHA-256 hash.

WebAuthn ceremony state (the challenge and the pending user) is kept in `IMemoryCache` under `flowId` for 5 minutes. F1 runs a single instance, so a memory cache is enough. If the app restarts mid-ceremony, the user simply retries.

**Why not ASP.NET Core Identity's passkeys:**

- Identity's passkey support is built around cookie sign-in.
- The PWA and the API sit on different sites (`*.azurestaticapps.net` and `*.azurewebsites.net`), and Safari blocks cross-site cookies.
- So the API issues bearer tokens itself and uses Fido2 (fido2-net-lib) for the WebAuthn verification.

**Relying party settings:**

- RP ID = the PWA's hostname (e.g. `meal-planner-abc123.azurestaticapps.net`); origin = `https://` + that hostname.
- Dev: RP ID `localhost`, origin `https://localhost:7002`.
- The browser runs the ceremony on the PWA's origin; the API verifies against these settings.

**Client side:**

- `js/interop.js` wraps `navigator.credentials.create` and `navigator.credentials.get`, converting between base64url strings and `ArrayBuffer` in both directions.
- `AuthService` keeps the `TokenPair` under the `mealplanner.auth` key.
- A `DelegatingHandler` attaches the access token and refreshes it 60 s before expiry.

### Sync protocol

```csharp
public sealed record SyncChange(string Type, Guid Id, string Json, bool IsDeleted, DateTime UpdatedAt, long Version = 0);
public sealed record SyncRequest(long Since, IReadOnlyList<SyncChange> Changes);
public sealed record SyncResponse(long ServerVersion, bool Full, IReadOnlyList<SyncChange> Changes);
```

`Type` is one of `Market`, `Ingredient`, `Meal`, `PlannedMeal`, `ShoppingList`, `ShoppingItem` (`EntityTypes` in Core). `Json` is the entity serialized with Core's shared `JsonSerializerOptions`.

**Server steps, in one transaction:**

1. **Validate:**
   - at most 500 changes
   - each `Json` ≤ 64 KB, deserializes to the Core record for `Type`, and has the same `Id`
   - otherwise 400
2. **Apply** each change in request order:
   - `household.Version++`
   - upsert the row in `Entities` with that `Version`, plus the client's `UpdatedAt`, `UpdatedBy = sub` and `IsDeleted`
   - "Last write wins" means the last one applied by the server.
3. **Purge,** at most once a day per household (checked via `LastPurgeAt`):
   - hard-delete rows with `IsDeleted` and `UpdatedAt` older than 60 days
   - raise `PurgedThroughVersion` to the highest `Version` deleted
4. **Respond:**
   - If `Since < PurgedThroughVersion`: return every non-deleted row and `Full = true`.
   - Otherwise: return every row with `Version > Since`, tombstones included, and `Full = false`.
   - `ServerVersion = household.Version`.

The response includes the caller's own just-applied changes, so the client learns their `Version`s.

### Configuration

| Setting | Dev | Production (App Service settings) |
| --- | --- | --- |
| `ConnectionStrings__Db` | `Data Source=mealplanner.db` | `Data Source=/home/data/mealplanner.db` |
| `Auth__JwtKey` | dev key in `appsettings.Development.json` | 64 random bytes, base64 |
| `Auth__RpId` | `localhost` | PWA hostname |
| `Auth__Origins__0` | `https://localhost:7002` | `https://<PWA hostname>` |
| `Cors__Origins__0` | `https://localhost:7002` | `https://<PWA hostname>` |

**On startup:**

- Create `/home/data` if it is missing.
- Run `Database.Migrate()`.
- Set `PRAGMA journal_mode=DELETE`. `/home` is a network share, and WAL mode is unsafe there.

CORS allows only the configured origins, methods `GET` and `POST`, and headers `Authorization` and `Content-Type`.

## Hosting and deployment

| Piece | Service | Notes |
| --- | --- | --- |
| PWA | Azure Static Web Apps, Free plan | Deployed from GitHub Actions with a deployment token. Turn off pull-request preview environments (passkeys only work on the production hostname). |
| API | Azure App Service, F1 Free, Linux, .NET 10 | Hosted in a US region: the owner's free subscription has no F1 quota in any EU region (decided 2026-10-10; Basic B1 in the EU, about $13/month, is the fallback). If the portal doesn't offer a .NET 10 stack, publish self-contained for `linux-x64`. F1 sleeps after about 20 min idle and has a daily CPU quota (about 60 min); two people use far less. |
| Database | SQLite file on the App Service `/home` share | Covered by Export in Settings. There are no automatic backups on F1. |
| CI | GitHub Actions | `ci.yml` builds and tests on every push and PR. `deploy.yml` runs on push to `main`: publish App → `Azure/static-web-apps-deploy` (`skip_app_build: true`, `app_location` = publish `wwwroot`); publish Api → `azure/webapps-deploy`. |

GitHub secrets:

- `AZURE_STATIC_WEB_APPS_API_TOKEN`
- `AZURE_WEBAPP_NAME`
- `AZURE_WEBAPP_PUBLISH_PROFILE` (needs SCM basic-auth publishing enabled on the web app)

Free-tier limits change. Check Microsoft's pricing pages before relying on exact numbers.

## Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| Passkeys are bound to the RP ID (the PWA hostname) | Changing the hostname later (custom domain, new Static Web App) invalidates every passkey | Keep the default hostname. If it must change, members join again via invite (data is household-owned, so nothing is lost). |
| F1 cold start | First sync after idle takes several seconds or more | Offline-first UI never waits for it; a 45 s timeout; `GET /api/health` on app start to wake the server. |
| SQLite on a network share | Locking problems if more than one process writes | F1 runs one instance; no WAL; small write volume. If problems appear, move to the Azure SQL Database free offer behind the same EF model. |
| iOS storage eviction | Local data lost | Home-screen web apps are not subject to Safari's 7-day eviction. The server holds everything, so after sign-in a `full` sync restores it. |
| Clock skew between phones | `UpdatedAt` is informational only | Ordering uses server `Version`, never client time. |
