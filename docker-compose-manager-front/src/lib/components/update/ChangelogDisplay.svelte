<script lang="ts">
  import { ChevronDown, ChevronRight, AlertTriangle, Shield, ExternalLink, Tag } from 'lucide-svelte';
  import { t } from '$lib/i18n';
  import Badge from '$lib/components/ui/badge.svelte';
  import type { ReleaseInfo, ChangelogSummary } from '$lib/types/update';

  interface Props {
    changelog: ReleaseInfo[];
    summary: ChangelogSummary;
    maxVisibleReleases?: number;
    class?: string;
  }

  let { changelog, summary, maxVisibleReleases = 5, class: className = '' }: Props = $props();

  let expandedReleases = $state<Set<string>>(new Set());
  let showAllReleases = $state(false);

  const visibleChangelog = $derived(
    showAllReleases ? changelog : changelog.slice(0, maxVisibleReleases)
  );

  const hasMoreReleases = $derived(changelog.length > maxVisibleReleases);

  function toggleRelease(version: string) {
    if (expandedReleases.has(version)) {
      expandedReleases.delete(version);
    } else {
      expandedReleases.add(version);
    }
    expandedReleases = new Set(expandedReleases);
  }

  function formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
</script>

<div class="space-y-4 {className}">
  {#if summary.totalReleases > 0}
    <!-- Summary badges -->
    <div class="flex flex-wrap gap-2 mb-4">
      <Badge variant="secondary">
        {summary.totalReleases} {summary.totalReleases === 1 ? $t('update.release') : $t('update.releases')}
      </Badge>
      {#if summary.hasSecurityFixes}
        <Badge variant="destructive">
          <Shield class="w-3 h-3 mr-1" />
          {$t('update.securityFixes')}
        </Badge>
      {/if}
      {#if summary.hasBreakingChanges}
        <Badge variant="warning">
          <AlertTriangle class="w-3 h-3 mr-1" />
          {$t('update.breakingChanges')}
        </Badge>
      {/if}
    </div>

    <!-- Release list -->
    <div class="space-y-2">
      {#each visibleChangelog as release (release.version)}
        {@const isExpanded = expandedReleases.has(release.version)}
        <div class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
          <!-- Release header -->
          <button
            type="button"
            class="w-full flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800/50 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors text-left"
            onclick={() => toggleRelease(release.version)}
          >
            <div class="flex items-center gap-3">
              {#if isExpanded}
                <ChevronDown class="w-4 h-4 text-gray-500" />
              {:else}
                <ChevronRight class="w-4 h-4 text-gray-500" />
              {/if}
              <div class="flex items-center gap-2">
                <Tag class="w-4 h-4 text-gray-500" />
                <span class="font-semibold text-gray-900 dark:text-gray-100">
                  v{release.version}
                </span>
              </div>
              <div class="flex gap-1">
                {#if release.isSecurityFix}
                  <Badge variant="destructive">
                    <Shield class="w-3 h-3" />
                  </Badge>
                {/if}
                {#if release.isBreakingChange}
                  <Badge variant="warning">
                    <AlertTriangle class="w-3 h-3" />
                  </Badge>
                {/if}
                {#if release.isPreRelease}
                  <Badge variant="secondary">{$t('update.preRelease')}</Badge>
                {/if}
              </div>
            </div>
            <span class="text-sm text-gray-500 dark:text-gray-400">
              {formatDate(release.publishedAt)}
            </span>
          </button>

          <!-- Release content (collapsible) -->
          {#if isExpanded}
            <div class="p-4 border-t border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
              {#if release.releaseNotes}
                <div class="prose prose-sm dark:prose-invert max-w-none">
                  <pre class="whitespace-pre-wrap text-sm text-gray-700 dark:text-gray-300 font-sans">{release.releaseNotes}</pre>
                </div>
              {:else}
                <p class="text-sm text-gray-500 dark:text-gray-400 italic">
                  {$t('update.noReleaseNotes')}
                </p>
              {/if}
              {#if release.releaseUrl}
                <div class="mt-3 pt-3 border-t border-gray-200 dark:border-gray-700">
                  <a
                    href={release.releaseUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    class="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                  >
                    {$t('update.viewOnGitHub')}
                    <ExternalLink class="w-3 h-3" />
                  </a>
                </div>
              {/if}
            </div>
          {/if}
        </div>
      {/each}
    </div>

    <!-- Show more/less button -->
    {#if hasMoreReleases}
      <button
        type="button"
        class="w-full py-2 text-sm text-primary hover:underline"
        onclick={() => showAllReleases = !showAllReleases}
      >
        {#if showAllReleases}
          {$t('update.showLess')}
        {:else}
          {$t('update.showMore', { count: changelog.length - maxVisibleReleases })}
        {/if}
      </button>
    {/if}
  {:else}
    <p class="text-sm text-gray-500 dark:text-gray-400">
      {$t('update.noChangelog')}
    </p>
  {/if}
</div>
