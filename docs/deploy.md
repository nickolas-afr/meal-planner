# Deploying to Azure

A one-time setup in the Azure portal, done by the owner. Afterwards every push to `main` deploys both parts through `.github/workflows/deploy.yml`.

| Piece | Azure service | Cost |
| --- | --- | --- |
| The PWA (`MealPlanner.App`) | Static Web App, Free plan | free |
| The API (`MealPlanner.Api`) and its SQLite file | App Service, Linux, F1 Free | free |

Free-tier limits change. Check Microsoft's pricing pages before relying on exact numbers.

Write down these values as you go; later steps need them:

| Name | Example | From step |
| --- | --- | --- |
| PWA hostname | `polite-river-0a1b2c3d4.azurestaticapps.net` | 2 |
| Static Web App deployment token | a long string | 2 |
| Web App name | `mealplanner-api-nick` | 3 |
| Web App default domain | `mealplanner-api-nick-abcd1234.westeurope-01.azurewebsites.net` | 3 |
| Publish profile | an XML file | 5 |

## 1. Resource group

1. In the [Azure portal](https://portal.azure.com), search for **Resource groups** and choose **Create**.
2. Name it `mealplanner` and pick a region near you, e.g. **West Europe**. The resource group's region is only where its metadata lives; the resources below can be in other regions.
3. **Review + create**, then **Create**.

## 2. Static Web App (the PWA)

1. Search for **Static Web Apps** and choose **Create**.
2. Resource group `mealplanner`, name `mealplanner`, plan type **Free**. The Free plan only offers a few regions (e.g. **East US 2**); any of them is fine, because the files are served from a global edge network.
3. **Deployment details → Source: Other.** GitHub Actions deploys the app; the portal must not create a workflow of its own.
4. **Review + create**, then **Create**, then **Go to resource**.
5. On **Overview**, copy the **URL**. The hostname without `https://` is your **PWA hostname**.
6. Choose **Manage deployment token** and copy the token.
7. Preview environments: under **Settings → Configuration** (or **Environments**), turn off pull request preview environments if the option is shown. The workflow only uploads from `main`, so previews shouldn't appear anyway. Passkeys only work on the production hostname.

Keep the default hostname. A custom domain added later would invalidate every passkey (architecture.md › Risks).

## 3. App Service (the API)

1. Search for **App Services** and choose **Create → Web App**.
2. Resource group `mealplanner`. Pick a globally unique name, e.g. `mealplanner-api-<yourname>`: this is your **Web App name**.
3. Publish **Code**, runtime stack **.NET 10 (LTS)**, operating system **Linux**, and a region where your subscription has **F1 quota**. Free accounts often have an F1 limit of 0 in every EU region; the create step then fails with "Current Limit (F1 VMs): 0". Check **Quotas → App Service** in the portal and pick a region with a limit of 1 or more. This household uses a US region (e.g. **East US 2**): syncs from Europe gain about 100 ms, which isn't noticeable, and the meal and shopping data is stored there.
4. **Pricing plan:** create a new Linux plan and pick **Free F1**.
5. Leave the other tabs at their defaults (no database, no Application Insights is fine). **Review + create**, then **Create**, then **Go to resource**.
6. On **Overview**, copy the **Default domain**: that is your **Web App default domain**.

If **Linux** has no F1 quota but **Windows** does, a Windows F1 Web App works too (this household's API runs on Windows F1 in Central US); use the Windows connection string in step 4.

If the portal doesn't offer a .NET 10 stack, stop and tell Claude: the workflow then has to publish self-contained for `linux-x64` (architecture.md › Hosting and deployment).

## 4. App Service settings

In the Web App, open **Settings → Environment variables → App settings** and add each of these with **+ Add**:

| Name | Value |
| --- | --- |
| `ConnectionStrings__Db` | Linux: `Data Source=/home/data/mealplanner.db`. Windows: `Data Source=D:\home\data\mealplanner.db` |
| `Auth__JwtKey` | a fresh key: run `openssl rand -base64 64 \| tr -d '\n' \| pbcopy` in Terminal (nothing is printed) and paste it |
| `Auth__RpId` | your PWA hostname, e.g. `polite-river-0a1b2c3d4.azurestaticapps.net` |
| `Auth__Origins__0` | `https://` + your PWA hostname |
| `Cors__Origins__0` | `https://` + your PWA hostname |

**Apply**, then **Confirm**: the app restarts. Re-open the list and check that all five are there: the portal sometimes drops values that were added but not applied. If a setting is missing or the key is malformed, the API refuses to start and the log stream names the setting. Never reuse the development key from `appsettings.Development.json`, and don't paste the production key anywhere else.

The API creates `/home/data` and the database on first start, and runs its migrations by itself.

## 5. Publish profile

1. In the Web App, open **Settings → Configuration → General settings**, turn **SCM Basic Auth Publishing Credentials** on, and **Save**. (The publish profile doesn't work without it.)
2. Back on **Overview**, choose **Download publish profile**. Keep the file private: it can deploy to your app.

## 6. GitHub secrets

In GitHub, open the repository → **Settings → Secrets and variables → Actions → New repository secret**, and add:

| Secret | Value |
| --- | --- |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | the deployment token from step 2 |
| `AZURE_WEBAPP_NAME` | the Web App name from step 3 |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | the whole contents of the publish profile file from step 5 |

Then delete the downloaded publish profile file.

## 7. Point the app at the API

Give Claude the **Web App default domain**. Claude sets it as `ApiBaseUrl` in `src/MealPlanner.App/wwwroot/appsettings.Production.json` and pushes to `main`. That push runs the first deployment (GitHub → **Actions → Deploy**).

Until all three secrets exist, the Deploy workflow skips its steps and stays green.

## 8. Check it

1. Open `https://<Web App default domain>/api/health`. It should say `ok`. The first request after the app has slept can take up to a minute.
2. Open `https://<PWA hostname>` on an iPhone in Safari. The Welcome screen should appear.
3. Continue with the smoke test in plan.md › M7.

## 9. Close sign-ups

Once everyone has joined the household, add one more app setting to the Web App: `Auth__AllowNewHouseholds` = `false`, then **Apply** and **Confirm**. Nobody else can then create a household with your API; members can still sign in, and invites still work. Set it back to `true` (or delete it) if you ever need a new household.

## Later

- **Data:** there are no automatic backups on F1. Use **Settings › Export data** in the app now and then.
- **Sleep:** F1 sleeps after about 20 minutes idle. The app pings `api/health` when it opens, so the first sync after a pause is a little slower, not broken.
- **Logs:** Web App → **Monitoring → Log stream** shows the API's output.
