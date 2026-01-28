<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';
  import type { HTMLAttributes } from 'svelte/elements';

  type Variant = 'default' | 'secondary' | 'destructive' | 'outline' | 'success' | 'warning';

  interface Props extends HTMLAttributes<HTMLDivElement> {
    variant?: Variant;
    children?: Snippet;
    class?: string;
  }

  let { variant = 'default', children, class: className, ...restProps }: Props = $props();

  const variantClasses: Record<Variant, string> = {
    default: 'bg-primary text-white',
    secondary: 'bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-gray-100',
    destructive: 'bg-danger text-white',
    outline: 'border border-gray-300 dark:border-gray-600 text-gray-900 dark:text-gray-100',
    success: 'bg-success text-white',
    warning: 'bg-warning text-white',
  };
</script>

<div
  class={cn(
    'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold transition-colors',
    variantClasses[variant],
    className
  )}
  {...restProps}
>
  {#if children}
    {@render children()}
  {/if}
</div>
