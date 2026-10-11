# Implementation plan

The work is in 8 milestones, built in order, in small slices for evenings and weekends. Each milestone ends with a working build, passing tests and a commit. The app runs locally without accounts until M5 (`Features:RequireAuth = false`) and goes live for both phones at M7.

Tick each box when the task is done. If a task turns out wrong or incomplete, update the docs first, as CLAUDE.md requires.

## M0 · Repository and skeleton

- [x] `git init` and a .NET `.gitignore`. Add a README with one paragraph and pointers to `docs/`.
- [x] `Directory.Build.props` with `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true` and `LangVersion latest`.
- [x] Create the projects from architecture.md › Solution layout:
  - `dotnet new classlib` (Core)
  - `dotnet new blazorwasm --pwa` (App, with the sample pages, sample data and Bootstrap removed)
  - `dotnet new web` (Api)
  - two `dotnet new xunit` (tests)
  - add all of them to `MealPlanner.sln`
- [x] Project references: App → Core, Api → Core, Core.Tests → Core, Api.Tests → Api.
- [x] `launchSettings.json`: App on `https://localhost:7002`, Api on `https://localhost:7001`.
- [x] `.github/workflows/ci.yml`:
  - triggers on every push and pull request
  - `actions/setup-dotnet` 10.x, then restore, build (`-warnaserror`) and test

**Done when:** `dotnet build` and `dotnet test` pass locally and in GitHub Actions, and the App runs and shows a blank shell.

## M1 · Core domain

- [x] Entities, exactly as in architecture.md › Core data model.
- [x] `Units`, `Weeks`, `Ids` (UUIDv5), `QuantityFormat` (`Format`, `TryParse`), `Validation`, `EntityTypes`, and a shared `JsonSerializerOptions`.
- [x] `ShoppingListBuilder`: `Build`, `IsOutOfDate`, `Merge`.
- [x] `ShoppingListLayout`: `ByMeal`, `ByMarket`, `Progress`.
- [x] `BackupFile` record and its (de)serialization.
- [x] Tests (Core.Tests), at least:
  - `Weeks.StartOf` for every weekday; `Weeks.Active` on Thursday vs Friday vs Sunday; `RetentionCutoff`
  - `Ids.ForItem` is deterministic and differs by week and by ingredient; it matches a known UUIDv5 test vector
  - `QuantityFormat.Format`: every example in architecture.md, plus 0.333 → "0.33" and 1000 → "1000"
  - `TryParse`: "1,5" and "1.5"; rejects 0, negatives, 3 decimals and "abc"; blank → null
  - `Build`: one meal; a meal planned twice doubles quantities; an ingredient shared by two meals sums with two sources; a null quantity sets `HasUnspecified`; all-null gives a null Quantity; deleted meals and ingredients are skipped; staple and market are copied
  - `IsOutOfDate`: identical input → false; added meal, removed meal, changed quantity, renamed ingredient, changed default market → true; changed default market when the item is overridden → false
  - `Merge`: keeps the tick when a quantity is unchanged or lower; unticks when it rises; removes no-longer-needed items; re-adds a deleted item; never returns manual items; keeps a market override
  - `ByMarket`: Anywhere first; a deleted market falls back to Anywhere; `ItemOrder` is respected, then alphabetical; ticked items last by `CheckedAt`; staples go to "Check you have"; empty groups are dropped
  - `ByMeal`: plan order; ×N; "Also in"; Extras; staples
  - `Progress` excludes staples and deleted items
  - Validation: each rule's boundaries

**Done when:** all of the above are implemented and green.

## M2 · Local app: meals, ingredients, markets

- [x] `wwwroot/css/tokens.css` (both themes), `app.css` (reset, global rules from ui.md), self-hosted Figtree `@font-face`.
- [x] Icon components (ui.md › Icons).
- [x] Shared components: `AppShell`, `TabBar`, `LargeTitleHeader`, `NavHeader`, `SearchField`, `RowGroup`, `Badge`, `BottomSheet`, `ActionSheet`, `ConfirmDialog`, `EmptyState`, `PrimaryButton`, `Toggle`, `Picker`, `FieldError`.
- [x] Storage: `IStore`, `LocalStorageStore`, `AppData` with `SchemaVersion = 1`.
- [x] `AppState`, `Clock`, `PreferencesService`.
- [x] `MealService`, `IngredientService`, `MarketService`, following the mutation steps in architecture.md › State and services. The outbox is filled even though nothing sends it yet.
- [x] App config `Features:RequireAuth` (default `false` until M5). When false, `NavGuard` lets everything through and `SyncService` is not registered.
- [x] Pages: Meals, Meal editor (with ingredient picker and inline create), Ingredients (with the ingredient sheet), and Settings › Markets only.

**Done when:**

- Meals and ingredients can be created, edited and deleted with every rule in spec.md › Meals, Ingredients and units, Markets, Deleting things and Validation.
- Data survives a reload.
- Light and dark themes both match ui.md.

## M3 · Plan and shopping

- [x] `PlanService`, including pruning of old weeks.
- [x] `ShoppingService`: `GetState`, `CreateOrUpdate` (uses `Build` and `Merge`; the list id comes from `Ids.ForList`), `Toggle` (sets or clears `CheckedAt`), `MoveToMarket`, and manual item add, edit and delete.
- [x] Plan page: week switcher, range limits, read-only past weeks, stepper, add list, floating button with its three states.
- [x] Shopping page:
  - header with progress and week ActionSheet
  - out-of-date banner and Update
  - both views via `ShoppingListLayout`
  - "Check you have"
  - "⋯" sheet and Move to market
  - add and edit item sheet
  - empty states
- [x] Reorder mode in By market: `wwwroot/js/interop.js` wraps SortableJS with `init(element, dotnetRef)` and `destroy`, and reports the new order of `data-id`s to .NET.
- [x] Ticked rows animate to the bottom (respecting `prefers-reduced-motion`).

**Done when:** you can plan a week (including ×2), create the list, tick items in both views, move an item to another market for the week, reorder a market, change the plan, see the banner, update, and get the results spec.md › Shopping list describes.

## M4 · PWA polish and backup

- [x] `manifest.webmanifest`, app icons:
  - a simple bowl mark on `#2E7A4B`
  - 192, 512 and maskable 512 PNGs, plus the 180 px `apple-touch-icon`
  - generated from one SVG in `icons/source.svg`
- [x] The `index.html` meta tags from architecture.md › PWA, including both theme colors.
- [x] Service worker update flow and the "New version available" banner.
- [x] `staticwebapp.config.json` as specified.
- [x] Settings › Backup (export, import with counts and confirm) and Settings › App (version, check for updates).
- [x] Accessibility pass against ui.md › Accessibility.
- [x] Manual check in Safari's responsive mode at 390 × 844 in both themes.

**Done when:**

- `dotnet publish -c Release` output served locally installs as a PWA and works with the network off.
- Export, then import, restores identical data.

## M5 · API and accounts

- [x] EF Core model and first migration for every table in architecture.md › Tables. Apply migrations on startup; set the SQLite pragma.
- [x] Options classes for `Auth` and `Cors`, CORS policy, JWT bearer authentication, and `HouseholdId` read from the `hid` claim.
- [x] `TokenService` (issue, refresh with rotation, revoke) and `PasskeyService` (Fido2: begin and complete for registration and login, ceremony state in `IMemoryCache`).
- [x] Endpoints: `health`, `auth/*`, `me`, `household/invites`, with the error codes in the endpoint table.
- [x] App:
  - WebAuthn interop in `interop.js`
  - `AuthService`: register, join, sign in, refresh, sign out
  - token `DelegatingHandler`
  - Welcome, Create and Join pages
  - Settings › Household (members, invite sheet with Share and Copy) and Settings › Account (Sign out)
  - Set `Features:RequireAuth = true`.
- [x] Api.Tests (`WebApplicationFactory`, SQLite in-memory): refresh rotation (an old token is rejected after use); invite expiry, single use and the 4-member limit; `me` returns only your household. Passkey verification itself is not unit-tested; test it manually on a device in M7.

**Done when:** locally, in Safari on a Mac with Touch ID or iCloud Keychain, you can create a household, invite, join from a second browser profile, sign out and sign in.

## M6 · Sync

- [x] Core `SyncContracts` and the `POST /api/sync` handler: validation, transactional apply with version stamping, daily purge, full or incremental response.
- [x] `SyncService`: triggers, the polling timer while visible (10 s on Shopping, 30 s elsewhere), the algorithm and error handling from architecture.md › SyncService, and the offline banner.
- [x] After sign-in or join, run a first sync with `since = 0`. A brand-new member starts with an empty local store; the server data fills it.
- [x] Import (M4) goes through the outbox, so it reaches the other phone.
- [x] Api.Tests:
  - two users of one household see each other's changes
  - another household's data is never returned
  - last write wins by arrival order
  - tombstones are returned
  - `since` below `PurgedThroughVersion` gives `Full = true`
  - oversize payloads and wrong types return 400
- [x] Core or App tests for the client merge: an incoming entity that is still in the outbox is skipped; a `full` response keeps outbox entities.

**Done when:** with two browser profiles, a change in one shows in the other within about 10 s while Shopping is open. An offline edit syncs after reconnecting. Both profiles creating the same week's list end up with one list.

## M7 · Deploy and go live

- [x] Write `docs/deploy.md`, a step-by-step guide for the owner to do in the Azure portal:
  - a resource group
  - a Static Web App on the Free plan (deployment source "Other"; copy the deployment token)
  - a Linux App Service plan F1 and a Web App (.NET 10, or self-contained)
  - app settings from architecture.md › Configuration, with a freshly generated JWT key
  - SCM basic-auth publishing enabled; download the publish profile
  - the GitHub secrets
- [x] `.github/workflows/deploy.yml` (architecture.md › Hosting and deployment). Set `wwwroot/appsettings.Production.json` `ApiBaseUrl` to the Web App URL.
- [ ] Smoke test on both iPhones:
  1. install from Safari
  2. create the household and join it
  3. add 3 meals, plan a week, create the list
  4. tick items on one phone and watch them on the other
  5. use airplane mode while ticking, then reconnect
  6. leave it for 30 min (cold start), then reopen
  7. publish a new version and see the update banner
  8. export a backup

**Done when:** both phones run the installed app from the production URL and every smoke test step passes.

## M8 · Several markets per ingredient, fewest markets per trip

Requested by the owner after go-live (2026-10-11). Rules in spec.md › Markets, Choosing markets and What an item shows; design in architecture.md › MarketChooser.

- [ ] Core:
  - `Ingredient.MarketIds` (priority order) with `DefaultMarketId` kept for older data; `IngredientMarkets.Of`
  - `ShoppingItem.IsNeeded`
  - `MarketChooser` (`Choose`, `Assign`)
  - `ShoppingListBuilder` (`Build` takes markets and the existing list; `IsOutOfDate` and `Merge` keep pinned items' markets and `IsNeeded`)
  - `ShoppingListLayout` (needed staples in the groups; `Progress`; `ChosenMarkets`)
  - `BackupFile` schema 2, still importing schema 1
- [ ] Core.Tests, at least:
  - `IngredientMarkets`: MarketIds, legacy DefaultMarketId, empty
  - `Choose`: one market sells everything → one market; two needed; fewest beats priority; priority breaks ties; Settings order breaks the rest; deleted markets ignored; staples and Anywhere items never add a market; pinned (manual, moved, needed, ticked) markets always included; greedy fallback above 12 markets
  - `Assign`: highest-priority chosen market; staple not sold at a chosen market → its first market
  - `Build`/`Merge` on Update: unticked items move when the choice changes, ticked items don't; `IsNeeded` survives
  - `IsOutOfDate`: a changed market choice for an unpinned item → true; for a ticked item → false
  - `Layout`: needed staple in its market group with a badge, counted in `Progress`; `ChosenMarkets`
  - Backup schema 1 import
- [ ] App:
  - `IngredientDraft.MarketIds`; `IngredientService` validates (live, no duplicates) and writes both fields
  - `MarketService.Delete` removes the market from ingredients
  - `ShoppingService.SetNeeded`; `CreateOrUpdate` passes markets and the existing list to `Build`
  - Ingredient sheet: Markets list with priority, drag to reorder, add and remove
  - Ingredients page and meal editor rows: first market (+N), "Also at …"
  - Shopping: chosen markets in the header; Need to buy / Don't need in the "⋯" sheet
- [ ] Sync check: data written by the new version loads on a phone still on the old one (first market shown), and old data loads on the new one.

**Done when:**

- A plan whose ingredients are all sold at one market gets a one-market list.
- A staple sold only at another market never adds that market, until it's marked Need to buy.
- Ticked items stay put through Update.
- Both phones show the same list after syncing.
