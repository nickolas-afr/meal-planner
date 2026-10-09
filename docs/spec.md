# Product spec

Meal Planner keeps a household's library of meals, lets either person pick the meals for a week, and turns that week into one shared shopping list, grouped by meal or by market. Both phones see the same data, and everything works offline.

## Users and devices

- One household: two people (the owner and his wife), each with an iPhone.
- The app is installed from Safari with Add to Home Screen and always used from the home-screen icon.
- Everything is shared inside the household: meals, ingredients, markets, plans and shopping lists. Nothing is private to one person, except the two per-phone preferences listed under Preferences.

## Glossary

| Term | Meaning |
| --- | --- |
| Meal | A named dish with an ingredient list and optional notes. Always cooked for 2; there is no servings count. |
| Ingredient | Something you buy, with one unit, an optional default market and a staple flag. |
| Market | A shop where you buy things (e.g. "Lidl", "Butcher"). Has a manual order of its items. |
| Anywhere | The pseudo-market for ingredients with no market set. Not stored as a market. |
| Staple | An ingredient you normally have at home (salt, oil, rice). Never listed for buying; shown in "Check you have". |
| Week | Monday to Sunday, in the phone's local time zone. Identified by its Monday (`WeekStart`). |
| Plan | The meals planned for one week. A meal planned twice counts twice. |
| Shopping list | One per week, generated from that week's plan, then ticked off and adjusted. |
| Active week | The week the Plan and Shopping tabs open on: the current week Monday to Thursday, the next week Friday to Sunday. |

## Meals

- A meal has a **name** (required), an **ingredient list** (zero or more rows) and **notes** (optional free text).
- Each ingredient row is an ingredient plus an optional **quantity** in that ingredient's unit. A blank quantity means "some" (e.g. parsley).
- The same ingredient cannot appear twice in one meal; the picker hides ingredients already in the meal.
- Ingredient rows keep the order they were added in. There is no reordering.
- Notes are discreet: collapsed behind an "Add notes" button in the editor and never shown on the shopping list.
- The library is expected to stay under 30 meals: one list with a search field, sorted by name.

## Ingredients and units

- An ingredient has a **name** (required, unique within the household, case-insensitive), a **unit**, a **default market** (optional; none means Anywhere) and a **staple** flag.
- The unit is picked from a fixed list: none, g, kg, ml, l, pc, clove, tsp, tbsp, cup, tin, pack, bunch, slice.
- All quantities for an ingredient are in its one unit, so totals are plain sums. There is no conversion.
- The unit cannot be changed while any meal uses the ingredient (the picker is shown locked, with the reason).
- Ingredients are created from the Ingredients tab or inline from the meal editor's ingredient picker.

## Markets

- Markets are an editable list in Settings: add, rename, reorder (drag) and delete.
- Each ingredient has at most one default market. Ingredients with none belong to Anywhere.
- Deleting a market moves its ingredients to Anywhere (they keep their names and units). Items on existing shopping lists that were in that market also show under Anywhere.
- Each market keeps a **manual item order**, set by dragging items in the shopping list's "By market" view. The order is stored per market and reused every week.

## Weeks and planning

- Weeks run Monday to Sunday.
- The Plan tab opens on the active week. Arrows move one week at a time, from 2 weeks back to 4 weeks ahead.
- Past weeks (before the current week) are read-only: they can be viewed, not changed.
- Adding a meal adds one plan entry. Adding the same meal again adds another entry; the row shows "×2" and the shopping list counts its ingredients twice.
- The row's stepper changes that count: minus removes one entry, and at 1 it removes the meal from the week.
- "Add from your meals" lists every meal not yet in that week, sorted by name, each with an add button.
- There are no suggestions, random weeks or copy-last-week.
- **Retention:** only the current week, the 2 weeks before it and future weeks are kept. Plans and shopping lists for older weeks are deleted automatically.

## Shopping list

### Creating and updating

- The Plan tab's main button creates the week's list. Its label depends on state:
  - No list for the week: **Create shopping list**.
  - List exists and matches the plan: **Open shopping list**.
  - List exists but the plan, a planned meal or an ingredient changed since: **Update shopping list**.
- A week has at most one list. If both phones create it at the same time, they produce the same list (see architecture: deterministic ids).
- When the list is out of date, the Shopping tab shows a banner: "Plan changed – Update list". Tapping Update applies these rules:
  - Ingredients still needed keep their tick, unless the total amount went up, in which case they are unticked.
  - Ingredients no longer needed are removed, ticked or not.
  - New ingredients are added unticked.
  - Manual items are never touched.
  - A per-week market move (below) is kept.
- The list never changes on its own; only Update changes it.

### What an item shows

- Each ingredient appears once, with its total: the sum of the quantities from every planned meal entry that uses it.
- Totals are exact sums, with no rounding: "1.5 pc", "750 g". At most 2 decimals, trailing zeros dropped, "." as the separator.
- If every source quantity is blank, no amount is shown. If some are blank and some are not, the known sum is shown followed by " +" (e.g. "2 pc +").
- Staple ingredients are not in the main groups. They sit in a collapsed **Check you have** section at the bottom, which can also be ticked.

### Views

- A segmented control switches between **By meal** and **By market**. Each phone remembers its own last choice; the first time, it opens on By meal.
- **By meal:** one group per planned meal, in the order the meals were first added to the week. A meal planned twice shows once, with "×2" and doubled amounts. Each row shows the amount needed for that meal. An ingredient used by several meals appears under each one, with "Also in: …", and ticking it in one place ticks it everywhere. Manual items are in an **Extras** group after the meals.
- **By market:** Anywhere first, then markets in their Settings order; empty groups are hidden. Each row shows the total and, underneath, the meal names that need it. Items follow the market's manual order; items not yet in that order follow alphabetically. Anywhere is always alphabetical and cannot be reordered.
- In both views, ticking an item **immediately moves it to the bottom of its group**, dimmed and struck through. Ticked items are ordered by the time they were ticked. Unticking returns an item to its normal place.
- The header shows the week and progress: "3 of 12 bought". Staples are not counted.

### Changing items on the list

- Tapping a row ticks or unticks it.
- Each row has a "⋯" button that opens a sheet:
  - **Move to market…** (all items): moves the item to another market, or Anywhere, for this week only. The ingredient's default market does not change.
  - **Edit** and **Delete** (manual items only).
- **+ Item** adds a manual item: name (required), quantity (optional), unit (from the unit list) and market (default Anywhere). Manual items are part of the week's list and belong to no meal.

## Sharing and accounts

- The first person creates the household: they enter their name and create a passkey with Face ID.
- They then make an **invite code** in Settings: 8 characters, valid for 48 hours, usable once. The second person chooses "I have an invite code", enters it and their name, and creates their own passkey. A household holds at most 4 people.
- On a new phone, **Sign in** uses the existing passkey (iCloud Keychain syncs passkeys between a person's Apple devices).
- If someone loses access to their passkey, another member makes a new invite and they join again as a new member. The household's data is not lost.
- Creating, joining and signing in need a connection. Everything else works offline.
- A session lasts until **Sign out**. You are not asked for Face ID on every open.

## Sync and offline

- Every change is saved on the phone first, so the app is fully usable with no signal.
- Changes reach the other phone automatically:
  - on app open and when the app returns to the foreground
  - about 1 second after each change
  - every 10 seconds while the Shopping tab is on screen, and every 30 seconds on the other tabs (only while the app is open and visible)
  - when the connection comes back
- While shopping together, a tick on one phone shows on the other within about 10 seconds.
- **Conflicts:** if both people change the same thing, the change that reaches the server last wins. "Same thing" means the same meal, ingredient, market, plan entry or shopping item. Two people ticking different items never conflict.
- If the phone is offline, or the last sync attempt failed more than 60 seconds ago, a slim banner says "Offline – changes will sync". When sync works, there is no indicator.

## Deleting things

| Action | Rule |
| --- | --- |
| Delete a meal | Asks to confirm. If the meal is planned in the current week or a future week, the dialog says so ("It's planned in 2 weeks; it will be removed from those plans.") and removes those plan entries. Past weeks are untouched. Affected shopping lists then show the update banner. |
| Delete an ingredient | Not allowed while any meal uses it. The sheet lists the meals that use it instead. Shopping items already on lists stay. |
| Delete a market | Asks to confirm ("Items from Lidl will move to Anywhere."). |
| Remove a meal from a plan | Stepper minus at count 1. No confirm. |
| Delete a manual item | From its "⋯" sheet. No confirm. |
| Leave the meal editor with unsaved changes | Asks "Discard changes?" with Discard and Keep editing. |

## Validation

| Field | Rule |
| --- | --- |
| Meal name | Required, 1–80 characters after trimming, unique among non-deleted meals (case-insensitive). |
| Ingredient name | Required, 1–60 characters after trimming, unique among non-deleted ingredients (case-insensitive). |
| Market name | Required, 1–40 characters, unique (case-insensitive). |
| Quantity | Blank, or a number > 0 and ≤ 100000 with at most 2 decimals. Accepts "," or "." as the decimal separator. |
| Shopping item name (manual items) | Required, 1–60 characters after trimming. Not unique. |
| Notes | Up to 2000 characters. |
| Display name | Required, 1–30 characters. |
| Invite code | 8 characters, case-insensitive, ignores spaces and dashes. |

Save buttons stay disabled until the form is valid. Errors show under the field in the danger colour.

If two phones create the same ingredient name offline, both copies survive the sync. The Ingredients tab shows them as two rows, and the user deletes one. No automatic merge.

## Preferences (per phone, not synced)

- The last shopping list view (By meal or By market).
- Appearance follows the phone's light or dark setting. There is no switch in the app.

## Backup

- **Settings › Export data** downloads `meal-planner-backup-YYYY-MM-DD.json`, containing every non-deleted entity and a schema version.
- **Settings › Import data** reads such a file, shows "Import 24 meals, 61 ingredients, 3 markets? This replaces everything for both of you.", and on confirm replaces the household's data. The change syncs to the other phone like any other.

## Defaults chosen without asking (confirm or change)

1. Active week switches to next week on Fridays.
2. Plan navigation: 2 weeks back (read-only) to 4 weeks ahead.
3. Ticked items are unticked by Update only when their total went up.
4. Item actions use a "⋯" button, not swipe gestures (swipes are unreliable in iOS home-screen web apps).
5. Units list as above.
6. Invite codes: 8 characters, 48 hours, single use; at most 4 members.
7. The app starts empty: no sample meals, ingredients or markets.
8. The app keeps its default Azure address. A custom domain added later would invalidate existing passkeys (see architecture, Risks).
