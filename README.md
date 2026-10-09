# Meal Planner

A meal planner and shared shopping list for one household of two people, each on an iPhone. It is an offline-first Blazor WebAssembly PWA with a small ASP.NET Core sync API, hosted on Azure free tiers. Start with [`docs/spec.md`](docs/spec.md) for the product rules, then [`docs/architecture.md`](docs/architecture.md), [`docs/ui.md`](docs/ui.md) and the milestone plan in [`docs/plan.md`](docs/plan.md).

## Local setup

The .NET SDK lives in the project, not on the machine. Install it once into the ignored `.tools/` folder, then load it in each shell:

```bash
.tools/dotnet-install.sh --channel 10.0 --install-dir .tools/dotnet --no-path   # get the script from https://dot.net/v1/dotnet-install.sh
source .tools/env.sh
dotnet build MealPlanner.sln && dotnet test
```
