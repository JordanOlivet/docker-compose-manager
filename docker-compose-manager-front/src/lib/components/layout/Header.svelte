<script lang="ts">
  import { Menu, LogOut } from 'lucide-svelte';
  import { goto } from '$app/navigation';
  import * as auth from '$lib/stores/auth.svelte';
  import { authApi } from '$lib/api';
  import { logger } from '$lib/utils/logger';
  import ThemeToggle from '$lib/components/common/ThemeToggle.svelte';
  import LanguageSelector from '$lib/components/common/LanguageSelector.svelte';
  import ConnectionStatus from '$lib/components/common/ConnectionStatus.svelte';
  import { t } from '$lib/i18n';

  interface Props {
    onToggleSidebar: () => void;
  }

  let { onToggleSidebar }: Props = $props();

  async function handleLogout() {
    try {
      const refreshToken = localStorage.getItem('refreshToken');
      if (refreshToken) {
        await authApi.logout(refreshToken);
      }
    } catch (error) {
      logger.error('Logout error:', error);
    } finally {
      auth.logout();
      goto('/login');
    }
  }
</script>

<header class="bg-white dark:bg-gray-800 shadow-md border-b border-gray-200 dark:border-gray-700 transition-all backdrop-blur-sm bg-opacity-95 relative z-[100]">
  <div class="flex items-center justify-between h-16 px-6">
    <div class="flex items-center gap-4">
      <button
        onclick={onToggleSidebar}
        class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-all duration-200 hover:scale-105"
        aria-label={$t('common.toggleSidebar')}
      >
        <Menu class="w-5 h-5 text-gray-600 dark:text-gray-300" />
      </button>

      <h1 class="text-xl font-bold text-transparent bg-clip-text bg-linear-to-r from-blue-600 to-blue-800 dark:from-blue-400 dark:to-blue-600 hidden sm:block">
        {$t('app.title')}
      </h1>
    </div>

    <div class="flex items-center gap-3">
      <ConnectionStatus />
      <LanguageSelector />
      <ThemeToggle />

      <div class="flex items-center gap-3 px-4 py-2 rounded-xl bg-linear-to-r from-gray-50 to-gray-100 dark:from-gray-700 dark:to-gray-800 shadow-sm">
        <div class="w-8 h-8 rounded-full bg-linear-to-br from-blue-500 to-blue-700 flex items-center justify-center text-white font-semibold text-sm shadow-md">
          {auth.auth.user?.username?.charAt(0).toUpperCase() || '?'}
        </div>
        <div class="hidden md:block">
          <span class="text-sm font-semibold text-gray-700 dark:text-gray-200 block">
            {auth.auth.user?.username || 'User'}
          </span>
          {#if auth.auth.user?.role}
            <span class="text-xs px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900 text-blue-700 dark:text-blue-200 font-medium">
              {auth.auth.user.role}
            </span>
          {/if}
        </div>
      </div>

      <button
        onclick={handleLogout}
        class="flex items-center gap-2 px-4 py-2 rounded-lg text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 transition-all duration-200 hover:scale-105 group"
        aria-label={$t('auth.logout')}
      >
        <LogOut class="w-4 h-4 group-hover:rotate-12 transition-transform duration-200" />
        <span class="text-sm font-medium hidden sm:inline">{$t('auth.logout')}</span>
      </button>
    </div>
  </div>
</header>
