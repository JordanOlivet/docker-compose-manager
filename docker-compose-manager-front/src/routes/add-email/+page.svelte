<script lang="ts">
	import { authApi } from '$lib/api/auth';
	import { goto } from '$app/navigation';
	import Input from '$lib/components/ui/input.svelte';
	import Button from '$lib/components/ui/button.svelte';
	import Card from '$lib/components/ui/card.svelte';
	import CardHeader from '$lib/components/ui/card-header.svelte';
	import CardTitle from '$lib/components/ui/card-title.svelte';
	import CardContent from '$lib/components/ui/card-content.svelte';
	import { Mail, Info } from 'lucide-svelte';

	let email = $state('');
	let isSubmitting = $state(false);
	let errorMessage = $state('');

	function validateEmail(email: string): boolean {
		const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
		return emailRegex.test(email);
	}

	async function handleSubmit(e: SubmitEvent) {
		e.preventDefault();

		if (!email.trim()) {
			errorMessage = 'Please enter your email address';
			return;
		}

		if (!validateEmail(email)) {
			errorMessage = 'Please enter a valid email address';
			return;
		}

		isSubmitting = true;
		errorMessage = '';

		try {
			await authApi.addEmail(email);
			goto('/');
		} catch (error: any) {
			errorMessage =
				error.response?.data?.message ||
				'Failed to add email. Email may already be in use.';
		} finally {
			isSubmitting = false;
		}
	}
</script>

<div class="min-h-screen flex items-center justify-center bg-linear-to-br from-gray-100 to-gray-200 dark:from-gray-900 dark:to-gray-800">
	<Card class="max-w-md w-full mx-4 shadow-xl">
		<CardHeader class="text-center pb-2">
			<div class="mx-auto w-16 h-16 bg-linear-to-br from-blue-500 to-blue-600 rounded-xl flex items-center justify-center shadow-lg mb-4">
				<Mail class="w-8 h-8 text-white" />
			</div>
			<CardTitle class="text-2xl font-bold">Add Your Email</CardTitle>
			<p class="text-sm text-gray-600 dark:text-gray-400 mt-2">
				Required for password reset functionality
			</p>
		</CardHeader>

		<CardContent class="pt-6">
			<div class="mb-4 p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
				<div class="flex items-start gap-3">
					<Info class="w-5 h-5 text-blue-600 dark:text-blue-400 flex-shrink-0 mt-0.5" />
					<div>
						<h3 class="text-sm font-semibold text-blue-800 dark:text-blue-300">Why do we need this?</h3>
						<p class="text-sm text-blue-700 dark:text-blue-400 mt-1">
							Your email address will be used to send password reset links if you forget your password. This is a one-time setup.
						</p>
					</div>
				</div>
			</div>

			{#if errorMessage}
				<div class="mb-4 p-3 bg-red-100 dark:bg-red-900/30 border border-red-400 dark:border-red-700 text-red-700 dark:text-red-300 rounded-lg text-sm">
					{errorMessage}
				</div>
			{/if}

			<form onsubmit={handleSubmit} class="space-y-4">
				<div>
					<label for="email" class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-2">
						Email Address
					</label>
					<Input
						type="email"
						id="email"
						bind:value={email}
						required
						autocomplete="email"
						placeholder="your.email@example.com"
					/>
				</div>

				<Button type="submit" disabled={isSubmitting} class="w-full">
					{#if isSubmitting}
						<span class="flex items-center justify-center gap-2">
							<span class="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
							Adding email...
						</span>
					{:else}
						Continue
					{/if}
				</Button>
			</form>

			<div class="mt-6 text-center text-sm text-gray-500 dark:text-gray-400">
				Your email will be kept private and only used for password recovery
			</div>
		</CardContent>
	</Card>
</div>
