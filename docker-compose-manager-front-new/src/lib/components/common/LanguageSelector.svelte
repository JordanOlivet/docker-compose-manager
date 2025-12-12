<script lang="ts">
  import { Globe } from 'lucide-svelte';
  import { locale, availableLocales, type Locale } from '$lib/i18n';

  interface Props {
    class?: string;
  }

  let { class: className = '' }: Props = $props();
  let isOpen = $state(false);
  let dropdownRef: HTMLDivElement | null = $state(null);

  const currentLanguage = $derived(
    availableLocales.find(lang => lang.code === $locale) || availableLocales[0]
  );

  function selectLocale(localeCode: Locale) {
    locale.set(localeCode);
    isOpen = false;
  }

  function handleClickOutside(event: MouseEvent) {
    if (dropdownRef && !dropdownRef.contains(event.target as Node)) {
      isOpen = false;
    }
  }
</script>

<svelte:window onclick={handleClickOutside} />

<div class="relative {className}" bind:this={dropdownRef}>
  <button
    onclick={() => isOpen = !isOpen}
    class="flex items-center gap-2 px-3 py-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-all duration-200 hover:scale-105"
    aria-label="Select language"
  >
    <Globe class="w-4 h-4 text-gray-600 dark:text-gray-300" />
    <span class="text-lg">{currentLanguage.flag}</span>
    <span class="text-sm font-medium text-gray-700 dark:text-gray-200 hidden sm:inline">
      {currentLanguage.code.toUpperCase()}
    </span>
  </button>

  {#if isOpen}
    <div class="absolute right-0 mt-2 w-48 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 py-2 z-[9999]">
      {#each availableLocales as language}
        <button
          onclick={() => selectLocale(language.code)}
          class="w-full flex items-center gap-3 px-4 py-2 text-left transition-colors {language.code === $locale
            ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400'
            : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/50'}"
        >
          <span class="text-xl">{language.flag}</span>
          <span class="text-sm font-medium">{language.name}</span>
          {#if language.code === $locale}
            <span class="ml-auto text-blue-600 dark:text-blue-400">✓</span>
          {/if}
        </button>
      {/each}
    </div>
  {/if}
</div>
