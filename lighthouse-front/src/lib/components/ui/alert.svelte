<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';

  type Variant = 'default' | 'destructive' | 'warning';

  interface Props {
    variant?: Variant;
    class?: string;
    children?: Snippet;
  }

  let { variant = 'default', class: className, children }: Props = $props();

  const variantClasses: Record<Variant, string> = {
    default: 'bg-blue-50 dark:bg-blue-950/50 border-blue-200 dark:border-blue-900 text-blue-900 dark:text-blue-200',
    destructive: 'bg-red-50 dark:bg-red-950/50 border-red-200 dark:border-red-900 text-red-900 dark:text-red-200',
    warning: 'bg-yellow-50 dark:bg-yellow-950/50 border-yellow-200 dark:border-yellow-900 text-yellow-900 dark:text-yellow-200'
  };
</script>

<div
  class={cn(
    'relative w-full rounded-lg border p-4',
    variantClasses[variant],
    className
  )}
  role="alert"
>
  {#if children}
    {@render children()}
  {/if}
</div>
