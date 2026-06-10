<script lang="ts">
  import type { ComponentType } from 'svelte';

  interface Props {
    icon: ComponentType;
    title?: string;
    variant?: 'play' | 'stop' | 'restart' | 'remove' | 'update' | 'force' | 'default';
    disabled?: boolean;
    loading?: boolean;
    onclick?: (e: MouseEvent) => void;
    class?: string;
    size?: 'sm' | 'md' | 'lg';
  }

  let {
    icon,
    title = '',
    variant = 'default',
    disabled = false,
    loading = false,
    onclick,
    class: className = '',
    size = 'sm'
  }: Props = $props();

  const variantClasses = {
    play: 'text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20',
    stop: 'text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20',
    restart: 'text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20',
    remove: 'text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20',
    update: 'text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20',
    force: 'text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20',
    default: 'text-gray-600 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-900/20'
  };

  const sizeClasses = {
    sm: 'p-1',
    md: 'p-1.5',
    lg: 'p-2'
  };

  const iconSizeClasses = {
    sm: 'w-4 h-4',
    md: 'w-5 h-5',
    lg: 'w-6 h-6'
  };
</script>

<button
  {title}
  {disabled}
  onclick={onclick}
  class="rounded transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed {sizeClasses[size]} {variantClasses[variant]} {className}"
>
  <svelte:component this={icon} class={iconSizeClasses[size]} />
</button>
