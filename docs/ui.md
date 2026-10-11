# UI spec

The app should look like a calm, native-feeling iPhone app: warm off-white ground, white rounded cards, one green accent, Figtree type and large titles. It follows the phone's light or dark setting.

The visual reference is the "Meal Planner Screens" design canvas in Claude. It was drawn before the final decisions, so where it disagrees with this file (servings steppers, "Serves" badges, "By aisle", Aisle pickers), this file wins.

## Design tokens

Define the tokens as CSS variables in `wwwroot/css/tokens.css`: light values on `:root`, dark values under `@media (prefers-color-scheme: dark)`. Components use only these variables.

| Token | Light | Dark | Used for |
| --- | --- | --- | --- |
| `--bg` | `#F5F4EF` | `#121412` | Page background |
| `--surface` | `#FFFFFF` | `#1B1E1B` | Cards, sheets |
| `--surface-2` | `#F2F1EC` | `#242824` | Stepper track, icon tiles, pressed rows |
| `--track` | `#E9E7E0` | `#242824` | Segmented control track |
| `--border` | `#E7E5DE` | `#2C312C` | Card and button borders |
| `--divider` | `#EFEDE6` | `#262A26` | Lines between rows |
| `--input-bg` | `#FBFAF7` | `#161916` | Text inputs |
| `--input-border` | `#D9D6CC` | `#3A403A` | Text input borders |
| `--text` | `#1C1E1B` | `#ECEDE8` | Primary text |
| `--text-2` | `#60645D` | `#A6AAA1` | Secondary text |
| `--text-3` | `#6A6E66` | `#8E928A` | Section labels, inactive tabs |
| `--icon-muted` | `#9A9D95` | `#6F746D` | Chevrons, drag handles |
| `--accent` | `#2E7A4B` | `#5DB483` | Primary buttons, active tab, checks, progress |
| `--accent-pressed` | `#1F5A36` | `#7CC79C` | Pressed primary |
| `--on-accent` | `#FFFFFF` | `#0D1A12` | Text and icons on accent |
| `--accent-tint` | `#E3EFE6` | `#1D3326` | "Planned" badge, monogram tiles, secondary buttons |
| `--on-accent-tint` | `#1F5A36` | `#A9DDBD` | Text on accent tint |
| `--staple-bg` | `#F3EAD3` | `#3A3120` | Staple badge |
| `--staple-text` | `#5B4A1E` | `#EAD7A4` | Staple badge text |
| `--danger` | `#B42318` | `#F08070` | Delete actions, field errors |
| `--checkbox` | `#B9BCB3` | `#5A605A` | Unticked circle border |
| `--checked-text` | `#7C8078` | `#7E837C` | Ticked item text |
| `--chrome` | `rgba(255,255,255,0.94)` | `rgba(27,30,27,0.94)` | Tab bar, sticky headers (with `backdrop-filter: blur(20px)`) |
| `--scrim` | `rgba(28,30,27,0.38)` | `rgba(0,0,0,0.55)` | Behind sheets and dialogs |

### Type

The font is Figtree, self-hosted (`wwwroot/fonts`, weights 400/500/600/700/800), falling back to `system-ui, sans-serif`.

| Style | Size / weight | Extra |
| --- | --- | --- |
| Large title | 34 / 800 | letter-spacing −0.02em, line-height 1.1 |
| Nav title | 17 / 700 | centred in nav header |
| Section title | 20 / 800 | letter-spacing −0.01em |
| Section label | 13 / 700 | uppercase, letter-spacing 0.06em, `--text-3` |
| Row title | 17 / 600 (meal rows), 16 / 500 (list rows) | |
| Body / inputs | 16 / 400 | inputs never below 16 px (stops iOS zoom on focus) |
| Secondary | 13–14 / 400 | `--text-2` |
| Badge | 11–12 / 700 | pill, padding 3px 8px |
| Tab label | 11 / 500, active 700 | |

### Shape and spacing

- **Page padding:** 20 px left and right; top padding is `env(safe-area-inset-top) + 14px`.
- **Gaps:** vertical gaps use 8, 10, 16, 18 or 22 px; rows inside a card are separated by 1 px `--divider` lines.
- **Radii:**
  - cards 16 px; meal cards and sheets 18 px (sheets: top corners only, 26 px)
  - inputs 12 px; pill buttons 22 px (44 px tall)
- **Touch targets** are at least 44 × 44 px. List rows are at least 48 px tall.
- **Tab bar:** 56 px of content plus `env(safe-area-inset-bottom)`, `--chrome` background, 1 px top border. Page content gets matching bottom padding.
- **Shadows:** only on sheets (`0 -8px 30px rgba(0,0,0,0.12)`) and the floating primary button on Plan (`0 6px 18px` accent at 28% opacity). No gradients anywhere.
- **Global CSS:** `html { -webkit-text-size-adjust: 100%; }`, `* { -webkit-tap-highlight-color: transparent; }`, and `touch-action: manipulation` on interactive elements.

### Icons

All icons are inline SVG components in `Components/Icons`, on a 24 × 24 viewBox, stroke `currentColor`, stroke-width 1.8, round caps and joins. The set:

| Group | Icons |
| --- | --- |
| Tabs | Bowl (Meals), Leaf (Ingredients), Calendar (Plan), Bag (Shopping) |
| Actions | Plus, PlusCircle, MinusCircle, Search, Sliders (Settings), Refresh, More (⋯), Download, Upload, Share, Trash |
| Navigation and controls | ChevronLeft, ChevronRight, ChevronDown, Check, DragHandle |
| Status | Lock, CloudOff, Note |

Icon-only buttons need an `aria-label`. Never use emoji.

## Components

Each component is a Razor file in `Components/`, with its CSS in a matching `.razor.css` file.

| Component | Behaviour |
| --- | --- |
| `AppShell` | Layout for signed-in pages: banner stack (top), page body, `TabBar` (bottom). Hides the tab bar on `/meals/new`, `/meals/{id}` and `/settings`. |
| `TabBar` | Four tabs: Meals `/meals`, Ingredients `/ingredients`, Plan `/plan`, Shopping `/shopping`. Active tab uses `--accent` and weight 700, with `aria-current="page"`. |
| `LargeTitleHeader` | Optional left icon button and right pill button row (44 px), then the large title and optional subtitle. |
| `NavHeader` | Sticky, 44 px tall plus the top safe area, `--chrome` background. Back link on the left (ChevronLeft + label, `--accent`), title in the centre, optional pill action on the right. |
| `SearchField` | 46 px, `--surface`, Search icon, `type="search"`, filters as you type (case-insensitive substring). |
| `Card` / `RowGroup` | `--surface`, 1 px `--border`, radius 16, rows separated by dividers. |
| `CheckRow` | A whole-row `<button role="checkbox" aria-checked>`. 24 px circle: unticked is a 2 px `--checkbox` border; ticked is filled `--accent` with a white Check. Title, optional subtitle, quantity on the right, then a "⋯" icon button outside the toggle area. Ticked state: text `--checked-text`, line-through. |
| `Stepper` | `--surface-2` track, two 36 px white buttons (Minus, Plus) around the value, `aria-label`s that name the meal. |
| `SegmentedControl` | 2 options, `--track` background, selected option is a white pill with a light shadow, 36 px tall. |
| `Badge` | Variants: Planned (accent tint), Staple (staple colours), Count "×2" (accent tint), Neutral (`--surface-2`). |
| `BottomSheet` | Slides up over a `--scrim`. Grab handle (38 × 5). Header row: Cancel (text, `--accent`) · title · Done/Save (accent pill). Closes on scrim tap or Cancel. Focus is trapped inside. Respects the bottom safe area. |
| `ActionSheet` | `BottomSheet` with a vertical list of 48 px option buttons plus Cancel. |
| `ConfirmDialog` | Centered card: title, message, two buttons. The destructive button uses `--danger` text. |
| `Banner` | Full-width strip under the safe area, 44 px. Variants: Offline (`--surface-2`, CloudOff icon, "Offline – changes will sync"); Update available (accent tint, "New version available", Reload button); List out of date (accent tint, "Plan changed", Update button). Several can stack. |
| `EmptyState` | Centred icon in a 56 px tile, title (17/700), one line of `--text-2`, optional primary button. |
| `PrimaryButton` | Accent fill, `--on-accent` text 15–17/700, radius 16 (full-width) or 22 (pill). Disabled at 40% opacity. |
| `Toggle` | iOS-style switch, 51 × 31, wrapping a real `<input type="checkbox">`. |
| `Picker` | Native `<select>` styled like an input (46 px). |
| `FieldError` | 13 px `--danger` text under a field, with the input's `aria-describedby` pointing to it. |

## Navigation and routes

| Route | Page | Signed in |
| --- | --- | --- |
| `/welcome` | Welcome | no |
| `/create` | Create household | no |
| `/join` | Join household | no |
| `/` | redirects to `/meals` | yes |
| `/meals` | Meals | yes |
| `/meals/new`, `/meals/{id:guid}` | Meal editor | yes |
| `/ingredients` | Ingredients | yes |
| `/plan?week=yyyy-MM-dd` | Plan (no `week` = active week) | yes |
| `/shopping?week=yyyy-MM-dd` | Shopping (no `week` = active week) | yes |
| `/settings` | Settings | yes |

`NavGuard` redirects to `/welcome` when there is no refresh token, and from the unauthenticated routes to `/meals` when there is one.

## Screens

### Welcome `/welcome`

From top to bottom, centred in the safe area:

- app icon (72 px)
- "Meal Planner" (34/800)
- the line "Plan the week together. Shop from one list."
- a 32 px gap

Below that, three actions:

1. **Create a household**: primary, full width. Navigates to `/create`.
2. **I have an invite code**: secondary (accent tint), full width. Navigates to `/join`.
3. **Sign in**: text button. Runs the passkey login ceremony right away. On success, does a full sync and navigates to `/meals`.

Offline: the buttons are disabled and a line reads "Connect to the internet to get started."

### Create household `/create`

- `NavHeader`: back to Welcome, title "Create household".
- Field: **Your name** (display name).
- Primary button **Continue with Face ID**: runs register/begin and complete without an invite.
- On success: save tokens, sync, go to `/meals`.
- Errors:
  - cancelled Face ID: "Passkey wasn't created. Try again."
  - network error: "Couldn't reach the server. Try again." (Both shown with `FieldError`.)

### Join household `/join`

- `NavHeader`: back to Welcome, title "Join household".
- Fields: **Invite code** (uppercase, monospace digits, auto-inserts the dash after 4 characters as "ABCD-EFGH"), then **Your name**.
- Primary button **Continue with Face ID**.
- Errors:
  - `invite_invalid`: "This code is wrong or has expired. Ask for a new one."
  - `household_full`: "This household is full."

### Meals `/meals`

- `LargeTitleHeader`:
  - left: Sliders icon button → `/settings`
  - right: pill **New meal** (Plus icon) → `/meals/new`
  - title **Meals**
  - subtitle "{n} meals · {p} planned this week", where p = distinct meals in the active week
- `SearchField` "Search meals".
- One card per meal, sorted by name, 10 px apart. Each card:
  - 48 px monogram tile: the first letters of the first two words, uppercase; `--accent-tint` if the meal is planned in the active week, otherwise `--surface-2`
  - name (17/600)
  - "{k} ingredients" in `--text-2`, a Note icon if the meal has notes, and a **This week** badge if planned
  - ChevronRight
  - Tapping the card goes to `/meals/{id}`.
- **Empty states:**
  - No meals: Bowl icon, "No meals yet", "Add the dishes you cook and what goes in them.", button **New meal**.
  - No search match: "No meals match "{query}"."

### Meal editor `/meals/new`, `/meals/{id}`

- `NavHeader`:
  - back label "Meals"
  - title "New meal" or "Edit meal"
  - right: pill **Save**, disabled until valid and changed
  - Back with unsaved changes opens the Discard dialog (spec › Deleting things).
- **Card 1:** field **Name** (17/600 text).
- **Ingredients section:**
  - Header row: "Ingredients" (20/800) with the count in `--text-2`, and a secondary pill **Add** (Plus) on the right.
  - A card with one row per ingredient, in the order added. Each row:
    - name (16/600), with its first market (plus " +1", " +2"… when it has more) or "Anywhere" in 13 `--text-2` below
    - a **Staple** badge if staple
    - a quantity input box: 40 px tall, `inputmode="decimal"`, right-aligned, placeholder "–", with the unit shown read-only to its right (fixed 44 px slot, empty when there is no unit)
    - a MinusCircle remove button (44 px, `--text-2`, `aria-label` "Remove {name}"), which removes the row without confirm
  - Empty: the line "No ingredients yet. Tap Add."
- **Notes:**
  - With no notes, a text button "Add notes" (Note icon, `--text-2`) below the ingredients card.
  - Tapping it reveals a "Notes" label and an auto-growing textarea (min 3 lines), focused.
  - With existing notes, the textarea shows from the start.
- **Delete meal:** existing meals only. A full-width button, `--surface` with `--danger` text. Opens the confirm dialog described in spec › Deleting things.
- **Ingredient picker** (BottomSheet "Add ingredient"):
  - `SearchField` focused, then ingredients not already in the meal, alphabetical. Each row shows name, unit and market; tapping one adds it with a blank quantity, closes the sheet and focuses the new row's quantity box.
  - If the search text matches no ingredient exactly, a row **Create "{text}"** appears first. It opens the ingredient sheet (below) pre-filled with that name; after Save, the new ingredient is added to the meal.

### Ingredients `/ingredients`

- `LargeTitleHeader`: right pill **New** → ingredient sheet (empty). Title **Ingredients**.
- `SearchField` "Search ingredients".
- Groups by first-priority market: **Anywhere** first, then markets in Settings order. Empty groups are hidden. Each group has a section label and a card.
- Each row (48 px):
  - name (16/500) on the left; when the ingredient has more markets, "Also at {Market B}, {Market C}" in 13 `--text-2` below
  - on the right: a **Staple** badge if staple, then the unit in `--text-2`
  - tapping a row opens the ingredient sheet
- **Ingredient sheet** (BottomSheet, title "New ingredient" or "Edit ingredient", Done disabled until valid):
  - **Name**
  - **Unit**: a Picker of `Units.All`, where "" shows as "None". Locked when the ingredient is used: greyed, with a Lock icon and the note "Unit is locked while {Meal A}, {Meal B} use this ingredient."
  - **Markets** (label, with the note "In order of preference" in 13 `--text-2`):
    - A card listing the chosen markets in priority order. Each row: a priority number (1, 2…), the market name, a DragHandle to reorder (SortableJS), and a MinusCircle remove button (`aria-label` "Remove {market}").
    - Below it, an **Add market** Picker listing the markets not yet chosen; picking one appends it.
    - With none chosen, the card shows "Anywhere".
    - With no markets in Settings at all, the section shows "No markets yet. Add them in Settings." instead.
  - **Pantry staple** Toggle, with the subtitle "Listed under "Check you have" instead"
  - For existing ingredients, a **Delete** button:
    - disabled with the label "Delete · used by {n} meals" when used
    - otherwise deletes after a confirm
- **Empty:** Leaf icon, "No ingredients yet", "They're also added from a meal.", button **New ingredient**.

### Plan `/plan`

- `LargeTitleHeader`: title **Plan**, no buttons.
- **Week switcher card:**
  - Previous and Next icon buttons (44 px on `--surface-2`), disabled at the range limits.
  - In the centre, the week name: "This week", "Next week", "Last week", or "Week of Oct 26". Below it, the range "Mon Oct 12 – Sun Oct 18".
  - Past weeks also show a Neutral badge "Read-only".
- **Planned section:**
  - Label "Planned", with "{n} meals" on the right (counting entries, so ×2 counts as 2).
  - One card row per distinct meal, in `AddedAt` order. Each row: name (16/600), a "{k} ingredients" subtitle, a ×N badge when N > 1, and a Stepper showing N.
  - Read-only weeks show no steppers.
  - Empty: "Nothing planned yet."
- **Add from your meals section** (hidden on read-only weeks):
  - A card of meals not in this week, sorted by name. Each row: name and a PlusCircle button (44 px, `--accent`, `aria-label` "Add {name}").
  - If every meal is planned: "Every meal is planned."
  - If there are no meals at all: a link "Add meals first" → `/meals`.
- **Floating primary button:**
  - Full width, 54 px tall, 16 px above the tab bar. Hidden when nothing is planned and no list exists, and on read-only weeks.
  - Label and action by state:
    - **Create shopping list**: creates the list, then goes to `/shopping?week=`
    - **Open shopping list**: goes to `/shopping?week=`
    - **Update shopping list**: updates the list, then goes to `/shopping?week=`
  - Content gets enough bottom padding to scroll clear of the button.

### Shopping `/shopping`

- **Header row:** right pill **Item** (Plus) → add-item sheet. Updating is done from the out-of-date banner only.
- **Title area:**
  - title **Shopping**
  - a row with the week name ("This week · 3 meals") on the left and "{bought} of {total} bought" (600) on the right
  - below it, the week's chosen markets from `ShoppingListLayout.ChosenMarkets`, e.g. "Lidl + Butcher" (13 `--text-2`); hidden when everything is under Anywhere
  - a 6 px progress bar (`--border` track, `--accent` fill)
  - The week name is a button that opens an ActionSheet of the retained weeks that have a list.
- **List out of date banner** at the top of the page when `IsOutOfDate`.
- **SegmentedControl:** **By meal** | **By market**, remembered per phone.
- **By meal:**
  - One card per meal. The header shows the meal name (17/700) and, if N > 1, a ×N badge.
  - Then `CheckRow`s: ingredient name, the "Also in: {other meals}" subtitle when shared, and this meal's quantity.
  - Then an **Extras** card for manual items.
  - Then **Check you have**.
- **By market:**
  - A section label per group ("Anywhere", then market names), with a text button **Reorder** on the right (not for Anywhere).
  - Then a card of `CheckRow`s: name, the subtitle "{meal}, {meal}" (or the market for manual items), and the total.
  - **Reorder mode:** the group's rows show a DragHandle and become draggable (SortableJS); ticking is disabled; the button reads **Done**. Done saves the order with `MarketService.SetItemOrder`.
- **Check you have** (both views):
  - A dashed-border button row: Staple badge, "Check you have", subtitle "{n} staples", and a ChevronDown.
  - Collapsed by default; expanding it shows its `CheckRow`s.
  - A staple marked **Need to buy** leaves this section and shows in the normal groups, with a **Staple** badge next to its name.
- **Ticking:** a row moves to the bottom of its group at once, with a 150 ms height and opacity transition (none when `prefers-reduced-motion` is set).
- **"⋯" ActionSheet:** **Move to market…** (opens a second ActionSheet: Anywhere, then markets, current one checked). For staples it also offers **Need to buy** (in Check you have) or **Don't need** (when already needed). For manual items it also offers **Edit** and **Delete**.
- **Add or edit item sheet** (BottomSheet, title "Add item" or "Edit item"):
  - Name (required)
  - Quantity (optional) and Unit (Picker) in two columns
  - Market (Picker, default Anywhere)
- **Empty:**
  - No list for the week: Bag icon, "No list for {week name}", "Plan some meals, then create the list.", button **Go to Plan** → `/plan?week=`.
  - List with no items: "Nothing to buy."

### Settings `/settings`

- `NavHeader`: back label "Meals", title "Settings".
- **Household** section:
  - A card with the household name, then one row per member (name, plus a "You" badge on your own row).
  - A row **Invite someone**: makes an invite and opens a sheet showing:
    - the code in 28/800 with letter-spacing 0.1em
    - "Valid until {weekday, time}"
    - a **Share** button (Web Share API: "Join our meal planner: code {code} at {app URL}")
    - **Copy**
  - Hidden at 4 members.
- **Markets** section:
  - A card with one row per market, in order: name, and a DragHandle on the right for drag-to-reorder (SortableJS, saves on drop). Tapping a name opens a sheet with **Name** and **Delete market**.
  - The last row is an input "Add a market" with an **Add** button.
  - Footer note: "Order them the way you shop. Items are grouped by these on the shopping list."
  - Empty: just the add row, with the note "No markets yet. Everything goes under Anywhere."
- **Backup** section:
  - **Export data** row (Download icon tile, subtitle "Saves everything as a .json file")
  - **Import data** row (Upload icon tile, subtitle "Replaces everything for both of you"). Opens `InputFile`, then the confirm dialog with counts.
  - Footer: "Last export: {date}" or "never". This is stored per phone in prefs.
- **Account** section: a row **Sign out** in `--danger`. It confirms "Sign out on this iPhone? Your household's data stays on the server.", then revokes the refresh token, clears local data and goes to `/welcome`.
- **App** section: **Version** row (assembly informational version), and **Check for updates** (asks the service worker to update; shows "You're up to date" or the update banner).

## Accessibility

- Real `<button>`, `<a>`, `<input>` and `<label>` everywhere. Never put `onclick` on a `div`.
- Every input has a visible label, or an `aria-label` when the context makes it obvious (quantity boxes: "{ingredient} quantity").
- Text contrast is at least 4.5:1 in both themes (the token values above meet this).
- Visible focus ring: 2 px `--accent` outline, offset 2 px, on `:focus-visible`.
- Sheets and dialogs trap focus, close on Escape, and return focus to the trigger.
