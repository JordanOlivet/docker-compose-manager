<script lang="ts">
	import { authApi } from '$lib/api/auth';
	import Input from '$lib/components/ui/input.svelte';
	import Button from '$lib/components/ui/button.svelte';
	import Card from '$lib/components/ui/card.svelte';
	import CardHeader from '$lib/components/ui/card-header.svelte';
	import CardTitle from '$lib/components/ui/card-title.svelte';
	import CardContent from '$lib/components/ui/card-content.svelte';
	import { KeyRound, CheckCircle2 } from 'lucide-svelte';

	let usernameOrEmail = $state('');
	let isSubmitting = $state(false);
	let isSuccess = $state(false);
	let errorMessage = $state('');

	async function handleSubmit(e: SubmitEvent) {
		e.preventDefault();
		if (!usernameOrEmail.trim()) {
			errorMessage = 'Please enter your username or email';
			return;
		}

		isSubmitting = true;
		errorMessage = '';

		try {
			await authApi.requestPasswordReset(usernameOrEmail);
			isSuccess = true;
		} catch (error: any) {
			errorMessage =
				error.response?.data?.message || 'An error occurred. Please try again later.';
		} finally {
			isSubmitting = false;
		}
	}
</script>

<div class="min-h-screen flex items-center justify-center bg-linear-to-br from-gray-100 to-gray-200 dark:from-gray-900 dark:to-gray-800">
	<Card class="max-w-md w-full mx-4 shadow-xl">
		<CardHeader class="text-center pb-2">
			<div class="mx-auto w-16 h-16 bg-linear-to-br from-blue-500 to-blue-600 rounded-xl flex items-center justify-center shadow-lg mb-4">
				<KeyRound class="w-8 h-8 text-white" />
			</div>
			<CardTitle class="text-2xl font-bold">Reset Your Password</CardTitle>
			<p class="text-sm text-gray-600 dark:text-gray-400 mt-2">
				Enter your username or email to receive a reset link
			</p>
		</CardHeader>

		<CardContent class="pt-6">
			{#if isSuccess}
				<div class="mb-4 p-4 bg-green-100 dark:bg-green-900/30 border border-green-400 dark:border-green-700 rounded-lg">
					<div class="flex items-start gap-3">
						<CheckCircle2 class="w-5 h-5 text-green-600 dark:text-green-400 flex-shrink-0 mt-0.5" />
						<div>
							<h3 class="text-sm font-semibold text-green-800 dark:text-green-300">Email sent!</h3>
							<p class="text-sm text-green-700 dark:text-green-400 mt-1">
								If an account exists, you will receive a password reset email shortly. Check your inbox and spam folder.
							</p>
						</div>
					</div>
				</div>

				<div class="text-center">
					<a
						href="/login"
						class="text-sm font-medium text-blue-600 hover:text-blue-500 dark:text-blue-400 dark:hover:text-blue-300"
					>
						← Back to login
					</a>
				</div>
			{:else}
				{#if errorMessage}
					<div class="mb-4 p-3 bg-red-100 dark:bg-red-900/30 border border-red-400 dark:border-red-700 text-red-700 dark:text-red-300 rounded-lg text-sm">
						{errorMessage}
					</div>
				{/if}

				<form onsubmit={handleSubmit} class="space-y-4">
					<div>
						<label for="usernameOrEmail" class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-2">
							Username or Email
						</label>
						<Input
							type="text"
							id="usernameOrEmail"
							bind:value={usernameOrEmail}
							required
							autocomplete="username"
							placeholder="Enter your username or email"
						/>
					</div>

					<Button type="submit" disabled={isSubmitting} class="w-full">
						{#if isSubmitting}
							<span class="flex items-center justify-center gap-2">
								<span class="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
								Sending...
							</span>
						{:else}
							Send Reset Link
						{/if}
					</Button>

					<div class="text-center">
						<a
							href="/login"
							class="text-sm font-medium text-blue-600 hover:text-blue-500 dark:text-blue-400 dark:hover:text-blue-300"
						>
							← Back to login
						</a>
					</div>
				</form>
			{/if}
		</CardContent>
	</Card>
</div>
