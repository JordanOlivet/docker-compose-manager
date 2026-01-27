<script lang="ts">
	import type { Snippet } from 'svelte';

	export interface InfoSection {
		id: string;
		title: string;
		icon?: Snippet;
		count?: number;
		initiallyOpen?: boolean;
		content: Snippet;
	}

	interface Props {
		title: string;
		sections: InfoSection[];
		headerActions?: Snippet;
		className?: string;
	}

	let { title, sections, headerActions, className = '' }: Props = $props();

	// Track open state per section
	let openStates = $state<Record<string, boolean>>(
		Object.fromEntries(sections.map((s) => [s.id, !!s.initiallyOpen]))
	);

	function toggle(id: string) {
		openStates[id] = !openStates[id];
	}
</script>

<div
	class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden {className}"
>
	<div
		class="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between"
	>
		<h3 class="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
			{title}
		</h3>
		{#if headerActions}
			<div class="flex items-center gap-2">
				{@render headerActions()}
			</div>
		{/if}
	</div>

	<div class="p-6 space-y-4">
		{#each sections as section}
			<div class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
				<button
					type="button"
					onclick={() => toggle(section.id)}
					class="flex items-center gap-2 w-full px-4 py-3 bg-gray-50 dark:bg-gray-700/50 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors text-left"
				>
					<!-- Chevron -->
					{#if openStates[section.id]}
						<span class="inline-block transition-transform">
							<svg
								class="h-4 w-4 text-gray-600 dark:text-gray-400"
								viewBox="0 0 24 24"
								fill="none"
								stroke="currentColor"
								stroke-width="2"
								stroke-linecap="round"
								stroke-linejoin="round"
							>
								<path d="M6 9l6 6 6-6" />
							</svg>
						</span>
					{:else}
						<span class="inline-block transition-transform -rotate-90">
							<svg
								class="h-4 w-4 text-gray-600 dark:text-gray-400"
								viewBox="0 0 24 24"
								fill="none"
								stroke="currentColor"
								stroke-width="2"
								stroke-linecap="round"
								stroke-linejoin="round"
							>
								<path d="M6 9l6 6 6-6" />
							</svg>
						</span>
					{/if}

					{#if section.icon}
						<span class="flex items-center">
							{@render section.icon()}
						</span>
					{/if}

					<span class="font-semibold text-gray-900 dark:text-white flex items-center gap-2">
						{section.title}
						{#if typeof section.count === 'number'}
							<span
								class="text-xs font-medium bg-gray-200 dark:bg-gray-600 text-gray-700 dark:text-gray-200 px-2 py-0.5 rounded-full"
							>
								{section.count}
							</span>
						{/if}
					</span>
				</button>

				{#if openStates[section.id]}
					<div class="p-4 bg-white dark:bg-gray-800">
						{@render section.content()}
					</div>
				{/if}
			</div>
		{/each}
	</div>
</div>
