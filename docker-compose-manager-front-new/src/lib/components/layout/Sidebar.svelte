<script lang="ts">
  import { page } from '$app/stores';
  import {
    LayoutDashboard,
    FileText,
    Package,
    Container,
    ClipboardList,
    Users,
    UsersRound,
    Shield,
    FileOutput,
    Settings,
    Boxes
  } from 'lucide-svelte';
  import { authStore } from '$lib/stores';
  import { t } from '$lib/i18n';

  interface Props {
    isOpen: boolean;
  }

  let { isOpen }: Props = $props();

  interface NavItem {
    to: string;
    icon: typeof LayoutDashboard;
    label: string;
    category?: string;
  }

  const adminNavItems: NavItem[] = [
    { to: '/', icon: LayoutDashboard, label: 'navigation.dashboard', category: 'navigation.categories.overview' },
    { to: '/compose/projects', icon: Package, label: 'navigation.composeProjects', category: 'navigation.categories.docker' },
    { to: '/containers', icon: Container, label: 'navigation.containers', category: 'navigation.categories.docker' },
    //{ to: '/logs', icon: FileOutput, label: 'navigation.logsViewer', category: 'navigation.categories.docker' },
    { to: '/users', icon: Users, label: 'navigation.userManagement', category: 'navigation.categories.administration' },
    { to: '/user-groups', icon: UsersRound, label: 'navigation.userGroups', category: 'navigation.categories.administration' },
    { to: '/permissions', icon: Shield, label: 'navigation.permissions', category: 'navigation.categories.administration' },
    { to: '/audit', icon: ClipboardList, label: 'navigation.auditLogs', category: 'navigation.categories.administration' },
    { to: '/compose/files', icon: FileText, label: 'navigation.composeFiles', category: 'navigation.categories.administration' },
    { to: '/settings', icon: Settings, label: 'navigation.settings', category: 'navigation.categories.administration' },
  ];

  const userNavItems: NavItem[] = [
    { to: '/', icon: LayoutDashboard, label: 'navigation.dashboard', category: 'navigation.categories.overview' },
    { to: '/compose/projects', icon: Package, label: 'navigation.composeProjects', category: 'navigation.categories.docker' },
    { to: '/containers', icon: Container, label: 'navigation.containers', category: 'navigation.categories.docker' },
    //{ to: '/logs', icon: FileOutput, label: 'navigation.logsViewer', category: 'navigation.categories.docker' },
  ];

  // Utiliser $derived pour la réactivité quand authStore.isAdmin change
  const navItems = $derived(authStore.isAdmin ? adminNavItems : userNavItems);

  const groupedNavItems = $derived(
    navItems.reduce((acc, item) => {
      const category = item.category ? t(item.category) : 'Other';
      if (!acc[category]) {
        acc[category] = [];
      }
      acc[category].push(item);
      return acc;
    }, {} as Record<string, NavItem[]>)
  );

  function isActive(path: string, currentPath: string): boolean {
    if (path === '/') {
      return currentPath === '/' || currentPath === '';
    }
    return currentPath.startsWith(path);
  }
</script>

{#if isOpen}
  <aside class="w-64 bg-white dark:bg-gray-900 border-r border-gray-200 dark:border-gray-800 flex flex-col shadow-lg transition-colors duration-200">
    <!-- Logo Header -->
    <div class="flex items-center h-16 px-6 border-b border-gray-200 dark:border-gray-800">
      <div class="flex items-center gap-3">
        <div class="p-2 bg-linear-to-br from-blue-500 to-blue-600 dark:from-blue-600 dark:to-blue-700 rounded-lg shadow-md">
          <Boxes class="w-5 h-5 text-white" />
        </div>
        <div>
          <span class="font-bold text-lg tracking-tight text-gray-900 dark:text-white">DCM</span>
          <p class="text-xs text-gray-500 dark:text-gray-400">{t('app.composeManager')}</p>
        </div>
      </div>
    </div>

    <!-- Navigation -->
    <nav class="flex-1 overflow-y-auto py-4">
      {#each Object.entries(groupedNavItems) as [category, items]}
        <div class="mb-6">
          <div class="px-6 mb-2">
            <h3 class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider">
              {category}
            </h3>
          </div>
          <ul class="space-y-1 px-3">
            {#each items as item}
              {@const active = isActive(item.to, $page.url.pathname)}
              {@const IconComponent = item.icon}
              <li>
                <a
                  href={item.to}
                  class="flex items-center gap-3 px-4 py-2.5 rounded-lg transition-all duration-200 group relative {active
                    ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400 shadow-sm'
                    : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800/50 hover:text-gray-900 dark:hover:text-white'}"
                >
                  <!-- Active indicator -->
                  {#if active}
                    <span class="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-8 bg-blue-600 dark:bg-blue-500 rounded-r-full"></span>
                  {/if}
                  <span class="transition-all duration-200 {active ? 'scale-110' : 'group-hover:scale-105'}">
                    <IconComponent class="w-5 h-5" />
                  </span>
                  <span class="font-medium text-sm">{t(item.label)}</span>
                </a>
              </li>
            {/each}
          </ul>
        </div>
      {/each}
    </nav>

    <!-- Footer -->
    <div class="p-4 border-t border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900/50">
      <div class="text-xs space-y-1">
        <p class="font-semibold text-gray-700 dark:text-gray-300">{t('app.title')}</p>
        <p class="text-gray-500 dark:text-gray-500">Version 0.1.0</p>
      </div>
    </div>
  </aside>
{/if}
