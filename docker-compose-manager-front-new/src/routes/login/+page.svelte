<script lang="ts">
  import { goto } from '$app/navigation';
  import { authStore } from '$lib/stores';
  import { authApi } from '$lib/api';
  import { t } from '$lib/i18n';
  import Input from '$lib/components/ui/input.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import CardContent from '$lib/components/ui/card-content.svelte';
  import PasswordInput from '$lib/components/common/PasswordInput.svelte';
  import { Boxes } from 'lucide-svelte';

  let username = $state('');
  let password = $state('');
  let error = $state('');
  let loading = $state(false);

  async function handleSubmit(e: SubmitEvent) {
    e.preventDefault();
    error = '';
    loading = true;

    try {
      const response = await authApi.login({ username, password });

      // Store tokens
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem('accessToken', response.accessToken);
        localStorage.setItem('refreshToken', response.refreshToken);
      }

      // Fetch complete user data
      const user = await authApi.getCurrentUser();

      // Update auth store
      authStore.login(response.accessToken, response.refreshToken, user);

      if (response.mustChangePassword) {
        goto('/change-password');
      } else {
        goto('/dashboard');
      }
    } catch (err: any) {
      if (typeof localStorage !== 'undefined') {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
      }
      error = err.response?.data?.message || t('auth.loginFailed');
    } finally {
      loading = false;
    }
  }
</script>

<div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-gray-100 to-gray-200 dark:from-gray-900 dark:to-gray-800">
  <Card class="max-w-md w-full mx-4 shadow-xl">
    <CardHeader class="text-center pb-2">
      <div class="mx-auto w-16 h-16 bg-gradient-to-br from-blue-500 to-blue-600 rounded-xl flex items-center justify-center shadow-lg mb-4">
        <Boxes class="w-8 h-8 text-white" />
      </div>
      <CardTitle class="text-2xl font-bold">{t('app.title')}</CardTitle>
      <p class="text-sm text-gray-500 dark:text-gray-400 mt-2">{t('auth.loginSubtitle')}</p>
    </CardHeader>
    <CardContent class="pt-6">
      {#if error}
        <div class="mb-4 p-3 bg-red-100 dark:bg-red-900/30 border border-red-400 dark:border-red-700 text-red-700 dark:text-red-300 rounded-lg text-sm">
          {error}
        </div>
      {/if}

      <form onsubmit={handleSubmit}>
        <div class="mb-4">
          <label for="username" class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-2">
            {t('auth.username')}
          </label>
          <Input
            type="text"
            id="username"
            bind:value={username}
            required
            autocomplete="username"
          />
        </div>

        <div class="mb-6">
          <label for="password" class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-2">
            {t('auth.password')}
          </label>
          <PasswordInput
            id="password"
            bind:value={password}
            required
            autocomplete="current-password"
          />
        </div>

        <Button type="submit" disabled={loading} class="w-full">
          {#if loading}
            <span class="flex items-center justify-center gap-2">
              <span class="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
              {t('auth.loggingIn')}
            </span>
          {:else}
            {t('auth.login')}
          {/if}
        </Button>
      </form>

      <div class="mt-6 text-center text-sm text-gray-600 dark:text-gray-400">
        <p>{t('auth.defaultCredentials')}</p>
        <p class="font-mono bg-gray-100 dark:bg-gray-800 p-2 mt-2 rounded text-gray-800 dark:text-gray-200">admin / adminadmin</p>
      </div>
    </CardContent>
  </Card>
</div>
