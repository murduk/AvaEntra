import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const idp = {
  target: "https://localhost:5100",
  secure: false,
  changeOrigin: true
};

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "../server/wwwroot",
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      "/api": idp,
      "/login": idp,
      "/oauth2": idp,
      "/v1.0": idp,
      "/oidc": idp,
      "/discovery": idp
    }
  }
});
