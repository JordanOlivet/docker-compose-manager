# Frontend component split — plan

Deferred from the cleanup pass because these are large **interactive** components and
the split can only be safely verified by running the app in a browser (svelte-check /
lint / build / unit tests catch types and obvious breakage, but not runtime reactivity
or visual regressions). Use this as the checklist when we tackle it with manual
verification available.

## Guiding rules

- **Behaviour-preserving extraction only** — move template + script chunks into child
  components / rune modules, wire props and `$bindable` state; don't redesign UX.
- One component (or one shared-logic extraction) per PR, each independently reviewable.
- After each PR: `npm run check` (0 errors), `npm run lint` (0 errors), `npm test`,
  `npm run build`, **and a manual click-through of the affected screen**.
- Add a unit test for every piece of **pure** logic pulled out into a `.ts`/`.svelte.ts`
  module (status helpers, progress rune, etc.). UI-only pieces are verified manually.
- Keep i18n keys, Tailwind classes and `bits-ui` usage identical when moving markup.

---

## 1. Settings page — `routes/(protected)/settings/+page.svelte` (~913 lines)

Already organised as a `Tabs` component with five `TabsContent` blocks. Each tab is a
self-contained section → extract one component per tab under
`lib/components/settings/`.

| Tab (`activeTab`) | Extract to | Notes |
|-------------------|-----------|-------|
| `general`         | `GeneralSettingsSection.svelte` | log level, language, theme |
| `update`          | `AppUpdateSection.svelte` | version info, changelog, update button, app auto-update card |
| `projectUpdate`   | `ProjectUpdateSection.svelte` | compose auto-update card + check settings |
| `notifications`   | `NotificationsSection.svelte` | Discord webhook settings |
| `registry`        | `RegistrySection.svelte` | registry credentials (already uses `registry/` components) |

Approach:
- Move each tab's markup + the script state it uses (queries, mutations, local state)
  into its section component; the page keeps only the tab shell + `activeTab`.
- Prefer each section owning its own TanStack queries/mutations rather than prop-drilling
  from the page.
- Target: page drops to ~120-150 lines (header + tab nav + 5 `<XSection />`).
- Verify: each tab renders, saves, and shows toasts exactly as before.

## 2. Compose projects page — `routes/(protected)/compose/projects/+page.svelte` (~794 lines)

Less cleanly sectioned than settings. Split into:
- `ComposeProjectsToolbar.svelte` — search / filter / bulk-update trigger.
- `ComposeProjectCard.svelte` (or `ComposeProjectRow`) — a single project's card incl.
  per-project actions (up/down/restart/update). This is the biggest win — the repeated
  per-project markup + action handlers.
- Keep list orchestration (query, SSE refresh, selection state) in the page; pass each
  project + callbacks to the card.
- Watch out for: the SSE-driven refresh and the batch-operation suppression
  (`batchOperation` store) must keep working; selection state likely stays in the page.
- Verify: list loads, per-project actions work, bulk update works, SSE updates reflect.

The sibling `compose/projects/[projectName]/+page.svelte` (~479 lines) can get the same
`ProjectInfoSection` / `ContainerInfoSection`-style treatment (those components already
exist — reuse, don't duplicate).

## 3. Update dialogs — shared logic + sub-components

`ServiceUpdateDialog` (724), `ContainerUpdateDialog` (570), `BulkUpdateDialog` (563)
share ~60%. Extract the shared pieces first, then thin each dialog.

### 3a. Pure status helpers → `lib/components/update/updateStatus.ts`
Identical across all three: `getStatusColor`, `getStatusBgColor`, `getStatusIcon`,
`getStatusLabel`, `getProgressBarColor`, and the `StatusIcon` mapping.
**Pure → unit-test it** (input status string → expected class/icon).

### 3b. Progress/logs rune → `lib/components/update/useUpdateProgress.svelte.ts`
The common live-update state: `updateProgress`, `updateLogs`, `logsExpanded`,
`restartAfterUpdate`, the SSE pull-progress subscription
(`unsubscribePullProgress`) and its cleanup, plus `batchOperation` start/end.
Expose a small API the dialogs consume. Unit-test the reducer-ish parts where possible.

### 3c. Shared sub-components (`lib/components/update/`)
- `UpdateStatusBadge.svelte` — uses 3a.
- `UpdateProgressBar.svelte`.
- `UpdateLogsPanel.svelte` — collapsible logs (`logsExpanded`, `logsContainer` autoscroll).
- `DigestCopyButton.svelte` — `copyToClipboard` + `copiedDigests` + `truncateDigest`.
- `ServiceSelectionList.svelte` — `allSelected` / `noneSelected` / `isSelected` /
  `selectAll` / `deselectAll` (shared by Service + Bulk dialogs).
- Common dialog chrome: `handleBackdropClick` / `handleKeydown` (Esc) — fold into the
  existing `ui/dialog` wrapper if not already there.

### 3d. Thin the three dialogs
Rewrite each to compose 3a-3c. Expect each to drop well under 300 lines, with only its
specific orchestration (which mutation, which entities) left.
Verify: run a real update via each dialog — progress, logs, restart toggle, digest copy,
multi-select, success/failure toasts all behave as before.

---

## 4. Stores `index.ts` consumers (optional)

The barrel now exports all stores (done in PR8a). Optionally migrate direct
`$lib/stores/x.svelte` imports to the barrel for consistency — low value, pure churn,
do only if touching those files anyway.

## 5. Repo-wide format (do LAST)

After the splits land, run `npm run format` (prettier) once as its **own** commit so the
large mechanical diff doesn't bury real changes. Then consider promoting the
burned-down ESLint rules from `warn` back to `error` in `eslint.config.js`
(`require-each-key`, `no-navigation-without-resolve`, `prefer-svelte-reactivity`,
`no-explicit-any`).

## 6. Lint-warning burndown (incremental)

~185 ESLint warnings remain (baseline kept green in PR3). Burn down by rule, e.g.:
- `state_referenced_locally` — wrap the flagged reads in `$derived`/closures.
- `require-each-key` — add `(item.id)` keys to `{#each}` blocks.
- `no-navigation-without-resolve` — use SvelteKit `resolve()` for `goto`/`href`.
- `no-explicit-any` — type the error handlers as `unknown` and narrow.

Each rule-batch is its own small PR; re-promote the rule to `error` once a category hits zero.
