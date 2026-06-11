import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ mode }) => {
	const env = loadEnv(mode, process.cwd(), '');
	const allowedHosts = env.ALLOWED_HOSTS
		? env.ALLOWED_HOSTS.split(',').map((h) => h.trim())
		: [];

	return {
		plugins: [sveltekit(), tailwindcss()],
		test: {
			environment: 'jsdom',
			globals: true,
			setupFiles: ['./vitest-setup.ts'],
			include: ['src/**/*.{test,spec}.{js,ts}'],
		},
		server: {
			...(allowedHosts.length && { allowedHosts }),
			proxy: {
				'/api': {
					target: 'http://localhost:5050',
					changeOrigin: true,
				},
			},
		},
	};
});
