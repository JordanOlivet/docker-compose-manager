<script lang="ts">
  import { CheckCircle, XCircle, User, Package, Container, FileText } from 'lucide-svelte';
  import { formatDistanceToNow } from 'date-fns';
  import { enUS, fr, es } from 'date-fns/locale';
  import { t, locale as localeStore } from '$lib/i18n';
  import type { Activity } from '$lib/api/dashboard';
  import { get } from 'svelte/store';

  interface Props {
    item: Activity;
  }

  let { item }: Props = $props();

  const iconMap: Record<string, typeof User> = {
    User,
    ComposeProject: Package,
    Container,
    ComposeFile: FileText
  };

  const localeMap: Record<string, typeof enUS> = { en: enUS, fr, es };

  const resourceIcon = $derived(iconMap[item.resourceType] || null);
  const currentLocaleCode = $derived(get(localeStore));
  const currentLocale = $derived(localeMap[currentLocaleCode] || enUS);
  const timeAgo = $derived(formatDistanceToNow(new Date(item.timestamp), {
    addSuffix: true,
    locale: currentLocale
  }));
</script>

<div class="flex items-start gap-4 p-6 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
  <div class="flex-shrink-0 p-3 rounded-full {item.success ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'}">
    {#if item.success}
      <CheckCircle class="w-5 h-5 text-green-600 dark:text-green-400" />
    {:else}
      <XCircle class="w-5 h-5 text-red-600 dark:text-red-400" />
    {/if}
  </div>
  <div class="flex-1 min-w-0">
    <p class="text-sm font-medium text-gray-800 dark:text-gray-100">
      <span class="font-semibold">{item.username}</span>
      <span class="text-gray-600 dark:text-gray-400">
        {t('common.performedAction', { action: item.action })}
      </span>
      {#if item.resourceType}
        <span class="text-gray-600 dark:text-gray-400">
          {t('common.onResource', { type: item.resourceType })}
        </span>
        {#if item.resourceId}
          <span class="font-mono text-blue-600 dark:text-blue-400">
            {item.resourceId}
          </span>
        {/if}
      {/if}
    </p>
    {#if item.details}
      <p class="text-xs text-gray-500 dark:text-gray-500 mt-1 truncate">
        {item.details}
      </p>
    {/if}
    <p class="text-xs text-gray-400 dark:text-gray-600 mt-1">
      {timeAgo}
    </p>
  </div>
  {#if resourceIcon}
    {@const IconComponent = resourceIcon}
    <div class="flex-shrink-0 text-gray-400 dark:text-gray-600">
      <IconComponent class="w-5 h-5" />
    </div>
  {/if}
</div>
