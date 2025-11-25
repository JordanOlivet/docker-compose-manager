<script lang="ts">
  import { Globe } from 'lucide-svelte';
  import { i18nStore, availableLocales, type Locale } from '$lib/i18n';

  interface Props {
    class?: string;
  }

  let { class: className = '' }: Props = $props();
  let isOpen = $state(false);

  function selectLocale(localeCode: Locale) {
    i18nStore.setLocale(localeCode);
    isOpen = false;
  }
</script>

<div class="relative {className}">
  <button
    onclick={() => isOpen = !isOpen}
    class="flex items-center gap-2 p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
    aria-label="Select language"
  >
    <Globe class="w-5 h-5 text-gray-600 dark:text-gray-400" />
    <span class="text-sm text-gray-600 dark:text-gray-400 uppercase">{i18nStore.locale}</span>
  </button>

  {#if isOpen}
    <div class="absolute right-0 mt-2 w-40 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 py-1 z-50">
      {#each availableLocales as loc}
        <button
          onclick={() => selectLocale(loc.code)}
          class="w-full px-4 py-2 text-left text-sm hover:bg-gray-100 dark:hover:bg-gray-700 {i18nStore.locale === loc.code ? 'bg-gray-100 dark:bg-gray-700' : ''}"
        >
          {loc.name}
        </button>
      {/each}
    </div>
  {/if}
</div>

<!-- Close dropdown when clicking outside -->
<svelte:window onclick={(e) => {
  const target = e.target as HTMLElement;
  if (!target.closest('.relative')) {
    isOpen = false;
  }
}} />
