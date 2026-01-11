# Frontend UI Changes for Feature F3: Compose Discovery UI

**Branch:** `feature/frontend-compose-ui`
**Status:** Implementation in progress
**Date:** 2026-01-10

## Summary

This document outlines all UI changes needed to support the new compose discovery features in the frontend. The backend now provides additional fields in the `ComposeProjectDto` to indicate whether projects have associated compose files and which actions are available.

## TypeScript Types - COMPLETED ✅

The `ComposeProject` interface in `src/lib/types/compose.ts` has already been updated with:
- `composeFilePath?: string | null` - Path to the compose file
- `hasComposeFile?: boolean` - Whether a file was found
- `warning?: string | null` - Warning message for issues
- `availableActions?: Record<string, boolean> | null` - Action availability dictionary

## UI Changes Needed

### 1. Project List Page (`src/routes/(protected)/compose/projects/+page.svelte`)

**Required Imports to Add:**
```typescript
import { AlertTriangle, FileText } from 'lucide-svelte';
import Badge from '$lib/components/ui/badge.svelte';
```

**New Helper Functions to Add:**
```typescript
// Check if an action is available
function isActionAvailable(project: ComposeProject, action: string): boolean {
  return project.availableActions?.[action] ?? true;
}

// Get tooltip text for disabled actions
function getDisabledTooltip(project: ComposeProject, action: string): string {
  if (project.availableActions?.[action]) return '';

  const requiresFile = ['up', 'build', 'recreate', 'pull'].includes(action);
  if (requiresFile && !project.hasComposeFile) {
    return 'No compose file found for this project';
  }

  return 'Action not available';
}
```

**UI Changes in Project Header Section:**

1. **Add "Not Started" Badge** (after StateBadge):
```svelte
{#if project.state === 'not-started'}
  <Badge variant="outline">
    Not Started
  </Badge>
{/if}
```

2. **Update Action Buttons with Availability Checks:**
- Add `disabled` attribute based on `isActionAvailable()`
- Add `disabled:opacity-50 disabled:cursor-not-allowed` classes
- Update title with conditional tooltip

Example for Up button:
```svelte
<button
  onclick={() => upMutation.mutate({ projectName: project.name })}
  disabled={!isActionAvailable(project, 'up')}
  class="p-1 text-green-600 ... disabled:opacity-50 disabled:cursor-not-allowed"
  title={isActionAvailable(project, 'up') ? $t('compose.up') : getDisabledTooltip(project, 'up')}
>
  <Play class="w-4 h-4" />
</button>
```

Apply to all action buttons: up, recreate (forceRecreate), restart, stop, down

3. **Update state conditions to include 'not-started':**
```svelte
{#if project.state === EntityState.Down || ... || project.state === 'not-started'}
```

4. **Add Compose File Path Display** (in metadata section):
```svelte
{#if project.composeFilePath}
  <div class="flex items-center gap-1 text-xs text-gray-600 dark:text-gray-400">
    <FileText class="w-3 h-3" />
    <span>File: {project.composeFilePath}</span>
  </div>
{/if}
```

5. **Add Warning Message Display** (after file path):
```svelte
{#if project.warning}
  <div class="flex items-center gap-2 text-xs text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-900/20 px-2 py-1 rounded">
    <AlertTriangle class="w-3 h-3" />
    <span>{project.warning}</span>
  </div>
{/if}
```

### 2. Project Detail Page (`src/routes/(protected)/compose/projects/[projectName]/+page.svelte`)

**Import to Add:**
```typescript
import { AlertTriangle, FileText } from 'lucide-svelte';
import Badge from '$lib/components/ui/badge.svelte';
```

**Changes in Project Header:**

1. **Add "Not Started" Badge** (if applicable)
2. **Apply same action availability logic** as list page
3. **Show compose file path prominently** in header
4. **Display warning if no file found**

**Helper Functions:**
Add the same `isActionAvailable()` and `getDisabledTooltip()` functions as in the list page.

### 3. Visual Design Guidelines

**Not-Started Badge:**
- Use `variant="outline"` for subtle appearance
- Placed next to state badge

**Warning Display:**
- Amber/yellow color scheme
- AlertTriangle icon
- Rounded background with padding
- Clearly visible but not alarming

**Disabled Actions:**
- 50% opacity
- Cursor not-allowed
- Tooltip explains why disabled
- Focus on user understanding

**File Path Display:**
- Small text with icon
- Gray/muted color
- FileText icon for clarity

## Testing Checklist

- [ ] Projects with state "not-started" show badge correctly
- [ ] Projects without compose files show warning message
- [ ] Compose file path displays when available
- [ ] Up/Build/Recreate buttons disabled for projects without files
- [ ] Start/Stop/Restart buttons remain enabled for all projects
- [ ] Tooltips explain why actions are disabled
- [ ] UI compiles without TypeScript errors
- [ ] Dark mode styling works correctly
- [ ] Responsive design maintained

## Implementation Notes

1. The types are already updated - no changes needed there
2. Focus on UI rendering and user feedback
3. Maintain existing functionality while adding new features
4. Follow existing component patterns (Badge, icons from lucide-svelte)
5. Ensure accessibility (titles, aria attributes)

## Backend API Contract

The backend now returns:
```typescript
{
  name: string,
  state: string,  // Can now be "not-started"
  composeFilePath?: string | null,
  hasComposeFile?: boolean,
  warning?: string | null,
  availableActions?: {
    up: boolean,
    down: boolean,
    restart: boolean,
    stop: boolean,
    start: boolean,
    pause: boolean,
    unpause: boolean,
    build: boolean,
    pull: boolean,
    push: boolean,
    logs: boolean,
    ps: boolean,
    recreate: boolean
  }
}
```

Actions requiring compose file (disabled if `hasComposeFile=false`):
- up, build, recreate, pull

Actions working with project name only (always enabled for running projects):
- start, stop, restart, pause, logs, ps, down

## Next Steps

1. Apply all changes to project list page
2. Apply changes to project detail page
3. Run `npm run build` to verify compilation
4. Test in browser if possible
5. Commit changes with descriptive message
6. Reference COMPOSE_DISCOVERY_SPECS.md in commit

---

**Note:** Due to file locking issues during implementation, the actual file modifications may need to be done manually based on this specification document.
