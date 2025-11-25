<script lang="ts">
  import { cn } from '$lib/utils';
  import type { HTMLButtonAttributes } from 'svelte/elements';
  import type { Snippet } from 'svelte';

  type Variant = 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
  type Size = 'default' | 'sm' | 'lg' | 'icon';

  interface Props extends HTMLButtonAttributes {
    variant?: Variant;
    size?: Size;
    class?: string;
    children?: Snippet;
  }

  let { variant = 'default', size = 'default', class: className, children, ...restProps }: Props = $props();

  const variantClasses: Record<Variant, string> = {
    default: 'bg-blue-600 text-white hover:bg-blue-700 shadow-sm',
    destructive: 'bg-red-600 text-white hover:bg-red-700 shadow-sm',
    outline: 'border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 text-gray-900 dark:text-gray-100',
    secondary: 'bg-gray-200 dark:bg-gray-700 text-gray-900 dark:text-gray-100 hover:bg-gray-300 dark:hover:bg-gray-600',
    ghost: 'hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-900 dark:text-gray-100',
    link: 'text-blue-600 dark:text-blue-400 underline-offset-4 hover:underline'
  };

  const sizeClasses: Record<Size, string> = {
    default: 'h-10 px-4 py-2',
    sm: 'h-9 rounded-md px-3 text-sm',
    lg: 'h-11 rounded-md px-8',
    icon: 'h-10 w-10'
  };
</script>

<button
  class={cn(
    'inline-flex items-center justify-center rounded-lg text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
    variantClasses[variant],
    sizeClasses[size],
    className
  )}
  {...restProps}
>
  {#if children}
    {@render children()}
  {/if}
</button>
