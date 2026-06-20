import { defineConfig } from "vite";

export default defineConfig({
  root: ".",
  server: {
    host: "127.0.0.1",
    port: 5174,
    strictPort: false
  },
  preview: {
    host: "127.0.0.1",
    port: 4174,
    strictPort: false
  }
});
