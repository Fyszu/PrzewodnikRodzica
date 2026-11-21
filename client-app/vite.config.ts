import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'https://localhost:7213',
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    outDir: '../PrzewodnikRodzica.Api/wwwroot',
    emptyOutDir: true,
  },
});