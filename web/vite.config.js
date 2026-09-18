import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    allowedHosts: [
      'wish-clarity-afford.ngrok-free.dev',
      '.ngrok-free.dev'
    ],
  },
});
