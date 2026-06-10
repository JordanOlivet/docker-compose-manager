<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';

  interface Props {
    class?: string;
    children?: Snippet;
  }

  let { class: className, children }: Props = $props();
</script>

<div class={cn('text-sm [&_p]:leading-relaxed', className)}>
  {#if children}
    {@render children()}
  {/if}
</div>
