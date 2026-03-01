<script lang="ts">
  import { dev } from '$app/environment';
  import { goto } from '$app/navigation';
  import { projectUpdateState } from '$lib/stores/projectUpdate.svelte';
  import { _devFirePullProgressUpdate } from '$lib/stores/sse.svelte';
  import BulkUpdateDialog from '$lib/components/update/BulkUpdateDialog.svelte';
  import type {
    CheckAllUpdatesResponse,
    ProjectUpdateSummary,
    UpdateProgressEvent,
    ServicePullProgress,
    ProjectUpdateRequest,
  } from '$lib/types/update';

  $effect(() => {
    if (!dev) goto('/');
  });

  // --- Config state ---
  let projectCount = $state(3);
  let servicesPerProject = $state(2);
  let durationMs = $state(5000);
  let failingProjectsInput = $state('project-b');
  let dialogOpen = $state(false);

  const failingProjects = $derived(
    failingProjectsInput.split(',').map(s => s.trim()).filter(Boolean)
  );

  // --- Fake data helpers ---
  function buildFakeCheckResult(count: number, services: number): CheckAllUpdatesResponse {
    const projects: ProjectUpdateSummary[] = Array.from({ length: count }, (_, i) => ({
      projectName: `project-${String.fromCharCode(97 + i)}`,
      servicesWithUpdates: services,
      lastChecked: new Date().toISOString(),
    }));
    return {
      projects,
      projectsChecked: count,
      projectsWithUpdates: count,
      totalServicesWithUpdates: count * services,
      checkedAt: new Date().toISOString(),
    };
  }

  function buildSimSteps(projectName: string, services: number): UpdateProgressEvent[] {
    const serviceNames = Array.from({ length: services }, (_, i) => `service-${i + 1}`);

    const makeServices = (statuses: Array<{ s: string; p: number }>): ServicePullProgress[] =>
      serviceNames.map((name, i) => ({
        serviceName: name,
        status: (statuses[i]?.s ?? 'waiting') as ServicePullProgress['status'],
        progressPercent: statuses[i]?.p ?? 0,
        message: null,
      }));

    const base = { operationId: `dev-op-${projectName}`, projectName, containerId: null };

    return [
      {
        ...base,
        phase: 'pull',
        overallProgress: 0,
        services: makeServices(serviceNames.map(() => ({ s: 'waiting', p: 0 }))),
        currentLog: null,
      },
      {
        ...base,
        phase: 'pull',
        overallProgress: 20,
        services: makeServices([{ s: 'pulling', p: 10 }, ...serviceNames.slice(1).map(() => ({ s: 'waiting', p: 0 }))]),
        currentLog: `Pulling image for ${serviceNames[0]}...`,
      },
      {
        ...base,
        phase: 'pull',
        overallProgress: 40,
        services: makeServices([{ s: 'downloading', p: 60 }, ...serviceNames.slice(1).map(() => ({ s: 'waiting', p: 0 }))]),
        currentLog: `Downloading layers for ${serviceNames[0]}...`,
      },
      {
        ...base,
        phase: 'pull',
        overallProgress: 55,
        services: makeServices([{ s: 'extracting', p: 80 }, ...serviceNames.slice(1).map(() => ({ s: 'pulling', p: 5 }))]),
        currentLog: `Extracting ${serviceNames[0]}, pulling ${serviceNames[1] ?? ''}...`,
      },
      {
        ...base,
        phase: 'pull',
        overallProgress: 65,
        services: makeServices([{ s: 'pulled', p: 100 }, ...serviceNames.slice(1).map(() => ({ s: 'downloading', p: 90 }))]),
        currentLog: `${serviceNames[0]} pulled successfully`,
      },
      {
        ...base,
        phase: 'pull',
        overallProgress: 80,
        services: makeServices(serviceNames.map(() => ({ s: 'pulled', p: 100 }))),
        currentLog: 'All images pulled',
      },
      {
        ...base,
        phase: 'recreate',
        overallProgress: 90,
        services: makeServices(serviceNames.map(() => ({ s: 'recreating', p: 0 }))),
        currentLog: 'Recreating containers...',
      },
      {
        ...base,
        phase: 'recreate',
        overallProgress: 100,
        services: makeServices(serviceNames.map(() => ({ s: 'completed', p: 100 }))),
        currentLog: 'Update complete',
      },
    ];
  }

  const sleep = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

  async function mockUpdateFn(
    projectName: string,
    _options: ProjectUpdateRequest
  ): Promise<void> {
    const shouldFail = failingProjects.includes(projectName);
    const steps = buildSimSteps(projectName, servicesPerProject);
    const stepDelay = Math.max(50, durationMs / steps.length);

    for (const step of steps) {
      await sleep(stepDelay);
      _devFirePullProgressUpdate(step);
    }

    if (shouldFail) {
      throw new Error(`[DEV] Simulated failure for ${projectName}`);
    }
  }

  function openDialog() {
    projectUpdateState.checkResult = buildFakeCheckResult(projectCount, servicesPerProject);
    dialogOpen = true;
  }

  function resetStore() {
    projectUpdateState.checkResult = null;
    dialogOpen = false;
  }

  const storeProjects = $derived(
    projectUpdateState.checkResult?.projects ?? []
  );
</script>

<div class="max-w-xl mx-auto p-8 space-y-8">
  <div>
    <h1 class="text-xl font-bold text-gray-900 dark:text-white">Bulk Update — Dev Test Page</h1>
    <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">Dev-only. Not visible in production.</p>
  </div>

  <!-- Config -->
  <section class="space-y-4 p-4 border border-dashed border-gray-300 dark:border-gray-600 rounded-lg">
    <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wide">Configuration</h2>

    <div class="grid grid-cols-2 gap-4">
      <label class="flex flex-col gap-1">
        <span class="text-xs text-gray-600 dark:text-gray-400">Projets</span>
        <input
          type="number"
          min="1"
          max="10"
          bind:value={projectCount}
          class="border border-gray-300 dark:border-gray-600 rounded px-2 py-1 text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white"
        />
      </label>

      <label class="flex flex-col gap-1">
        <span class="text-xs text-gray-600 dark:text-gray-400">Durée (ms)</span>
        <input
          type="number"
          min="0"
          step="500"
          bind:value={durationMs}
          class="border border-gray-300 dark:border-gray-600 rounded px-2 py-1 text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white"
        />
      </label>
    </div>

    <label class="flex flex-col gap-1">
      <span class="text-xs text-gray-600 dark:text-gray-400">Services par projet</span>
      <input
        type="number"
        min="1"
        max="8"
        bind:value={servicesPerProject}
        class="border border-gray-300 dark:border-gray-600 rounded px-2 py-1 text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white"
      />
    </label>

    <label class="flex flex-col gap-1">
      <span class="text-xs text-gray-600 dark:text-gray-400">Projets en échec (virgule-séparés)</span>
      <input
        type="text"
        bind:value={failingProjectsInput}
        placeholder="project-b, project-c"
        class="border border-gray-300 dark:border-gray-600 rounded px-2 py-1 text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white"
      />
    </label>
  </section>

  <!-- Actions -->
  <div class="flex gap-3">
    <button
      onclick={openDialog}
      class="px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors cursor-pointer"
    >
      Ouvrir le dialog
    </button>
    <button
      onclick={resetStore}
      class="px-4 py-2 text-sm font-medium rounded-lg bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors cursor-pointer"
    >
      Reset le store
    </button>
  </div>

  <!-- Store state -->
  <section class="space-y-2">
    <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wide">État du store</h2>
    {#if storeProjects.length === 0}
      <p class="text-sm text-gray-400 italic">Store vide</p>
    {:else}
      <ul class="space-y-1">
        {#each storeProjects as p (p.projectName)}
          <li class="text-sm text-gray-700 dark:text-gray-300">
            • <span class="font-mono font-medium">{p.projectName}</span>
            — {p.servicesWithUpdates} services
            {#if failingProjects.includes(p.projectName)}
              <span class="text-red-500 text-xs ml-1">(échec simulé)</span>
            {/if}
          </li>
        {/each}
      </ul>
    {/if}
  </section>
</div>

<BulkUpdateDialog
  open={dialogOpen}
  onClose={() => { dialogOpen = false; }}
  updateFn={mockUpdateFn}
/>
