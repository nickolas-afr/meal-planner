# Meal Planner

A meal planner and shared shopping list for one household, used by two people on their iPhones. Plan the week together, then shop from one list that stays in sync on both phones.

It is an offline-first Blazor WebAssembly PWA (installed from Safari to the Home Screen) with a small ASP.NET Core sync API, hosted on Azure free tiers. Everything works offline; changes sync when there is a connection.

**Status:** all milestones M0–M7 are done and the app is live. See [`docs/plan.md`](docs/plan.md).

## Features

- **Meals:** a library of dishes with ingredients, quantities and notes.
- **Ingredients and markets:** one unit per ingredient, a default market, and a "staple" flag for things you usually have at home.
- **Plan:** a week at a time, from 2 weeks back to 4 weeks ahead; a meal can be planned more than once (×2).
- **Shopping list:** built from the plan with exact totals, viewed by meal or by market, ticked off together, with manual items, per-week market moves, your own item order per market, and an update banner when the plan changes.
- **Household:** sign-in with passkeys (Face ID); invite the other person with an 8-character code.
- **Sync and offline:** every change is saved on the phone first and synced in the background (every 10 s on Shopping, 30 s elsewhere).
- **Backup:** export everything to a `.json` file and import it again.

Out of scope on purpose: servings and scaling, unit conversion, photos, tags, recipe import, suggestions, notifications, multiple households, translations, iPad or desktop layouts.

## Docs

| File | What it holds |
| --- | --- |
| [`docs/spec.md`](docs/spec.md) | Product rules: what the app does, in every case. Authoritative. |
| [`docs/architecture.md`](docs/architecture.md) | Projects, data model, algorithms, sync protocol, auth, hosting. |
| [`docs/ui.md`](docs/ui.md) | Design tokens, components and every screen. |
| [`docs/plan.md`](docs/plan.md) | Milestones M0–M7 and their checks. |
| [`docs/deploy.md`](docs/deploy.md) | One-time Azure setup and day-to-day operations. |

If code and docs disagree, the docs win.

## Repository layout

```
src/MealPlanner.Core     domain records and all logic (shopping list builder, layout, sync merge, validation)
src/MealPlanner.App      Blazor WebAssembly PWA: pages, components, services, local storage
src/MealPlanner.Api      ASP.NET Core minimal API: passkeys (Fido2), JWT, households, invites, sync; EF Core + SQLite
tests/                   xUnit: Core.Tests (domain) and Api.Tests (integration, in-memory SQLite)
icons/                   source.svg and build.sh for the app icons
.github/workflows        ci.yml (build + test on every push/PR), deploy.yml (deploy on push to main)
```

## Local setup

Everything is installed inside the repo under `.tools/`, never machine-wide (`.tools/` is gitignored except `env.sh`).

1. **Install the .NET 10 SDK into `.tools/`** (once; the version is pinned by `global.json`):

   ```bash
   curl -sSL https://dot.net/v1/dotnet-install.sh -o .tools/dotnet-install.sh
   bash .tools/dotnet-install.sh --channel 10.0 --install-dir .tools/dotnet --no-path
   ```

2. **Load the tools in every shell:**

   ```bash
   source .tools/env.sh
   ```

3. **Optional tools,** also under `.tools/`: `dotnet tool install dotnet-ef --tool-path .tools/ef` for migrations, and the GitHub CLI unpacked to `.tools/gh` for checking CI runs.

4. **Trust the HTTPS dev certificate** (once; passkeys need HTTPS):

   ```bash
   dotnet dev-certs https --trust
   ```

## Build, test, run

```bash
dotnet build MealPlanner.sln
dotnet test
dotnet watch --project src/MealPlanner.Api   # https://localhost:7001
dotnet watch --project src/MealPlanner.App   # https://localhost:7002
```

Open `https://localhost:7002` in Safari. Sign-in uses passkeys with the relying party `localhost`, so Touch ID or iCloud Keychain works on a Mac; use a second Safari profile to try joining with an invite. The local API keeps its data in `src/MealPlanner.Api/mealplanner.db` (gitignored).

New EF Core migration:

```bash
dotnet ef migrations add <Name> --project src/MealPlanner.Api
```

App icons: edit `icons/source.svg`, then run `icons/build.sh` (macOS built-ins only).

## Deployment

- **PWA:** Azure Static Web Apps.
- **API:** Azure App Service, with SQLite on the app's persistent `D:\home` storage.
- **CI/CD:** every push to `main` runs [`deploy.yml`](.github/workflows/deploy.yml), which builds, tests and publishes both parts. It needs four repository secrets: `AZURE_STATIC_WEB_APPS_API_TOKEN`, `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_PUBLISH_PROFILE` and `API_BASE_URL`.
- **Setup and settings:** the full Azure setup, the API's app settings (JWT key, relying party, origins) and closing sign-ups after everyone has joined are in [`docs/deploy.md`](docs/deploy.md).

## Operations

- **Sleep:** the free API sleeps after about 20 minutes idle. The app wakes it on open, so the first sync after a pause just takes a little longer.
- **Monitoring:** Web App → Overview charts, Monitoring → Metrics and Log stream.
