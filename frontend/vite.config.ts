import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// Бэкенд ChatNode по умолчанию слушает http://localhost:5064 (см. Properties/launchSettings.json).
// Проксируем /api и /chat-hub, чтобы в дев-режиме не было CORS и всё жило на одном origin.
export default defineConfig(({ mode }) => {
  // '.' — корень проекта; так обходимся без @types/node ради одного process.cwd()
  const env = loadEnv(mode, '.', '');
  const target = env.VITE_API_TARGET || 'http://localhost:5064';

  return {
    plugins: [react()],
    server: {
      // Явный IPv4: по умолчанию Node на Windows умеет резолвить localhost
      // только в ::1, и часть инструментов до дев-сервера не достучится.
      host: '127.0.0.1',
      port: 5173,
      proxy: {
        '/api': { target, changeOrigin: true, secure: false },
        '/chat-hub': { target, changeOrigin: true, secure: false, ws: true },
      },
    },
    build: {
      outDir: 'dist',
      sourcemap: false,
    },
  };
});
