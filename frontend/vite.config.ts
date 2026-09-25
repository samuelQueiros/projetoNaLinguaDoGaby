import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/react/',
  build: { outDir: '../backend/src/ErpFinanceiro.Web/wwwroot/react', emptyOutDir: true },
  server: { allowedHosts: ['chat-server'], proxy: { '/api': 'http://localhost:5080', '/anexos': 'http://localhost:5080', '/relatorios': 'http://localhost:5080', '/documentos-importados': 'http://localhost:5080' } }
});
