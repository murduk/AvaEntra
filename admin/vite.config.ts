import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "../server/wwwroot",
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5100",
      "/login": "http://localhost:5100",
      "/oauth2": "http://localhost:5100",
      "/v1.0": "http://localhost:5100",
      "/oidc": "http://localhost:5100",
      "/discovery": "http://localhost:5100"
    }
  }
});
