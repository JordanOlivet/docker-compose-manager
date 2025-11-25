import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
const config = {
	// Consult https://svelte.dev/docs/kit/integrations
	// for more information about preprocessors
	preprocess: vitePreprocess(),

	kit: {
		adapter: adapter({
			pages: 'dist',
			assets: 'dist',
			fallback: 'index.html',
			precompress: false,
			strict: true
		}),
		alias: {
			$components: 'src/lib/components',
			$stores: 'src/lib/stores',
			$api: 'src/lib/api',
			$types: 'src/lib/types',
			$utils: 'src/lib/utils',
			$i18n: 'src/lib/i18n'
		}
	}
};

export default config;
