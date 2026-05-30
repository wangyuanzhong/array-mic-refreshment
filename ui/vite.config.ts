import { defineConfig } from 'vite';
import { resolve } from 'path';

export default defineConfig({
  base: './',
  build: {
    outDir: resolve(__dirname, '../src/ArrayMicRefreshment.App/wwwroot'),
    emptyOutDir: true,
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html'),
        hud: resolve(__dirname, 'hud.html'),
      },
    },
  },
});
