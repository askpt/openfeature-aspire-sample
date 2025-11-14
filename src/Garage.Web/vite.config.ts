import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");

  const ofrepServiceUrl = process.env.services__flagd__ofrep__0;

  // Only define OFREP service URL if it is set
  const defineConfig: any = {};
  if (ofrepServiceUrl) {
    defineConfig["import.meta.env.VITE_OFREP_SERVICE_URL"] =
      JSON.stringify(ofrepServiceUrl);
  }

  return {
    plugins: [react()],
    server: {
      port: parseInt(env.VITE_PORT),
      proxy: {
        "/api": {
          target:
            process.env.services__apiservice__https__0 ||
            process.env.services__apiservice__http__0,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/api/, ""),
          secure: false,
        },
      },
    },
    build: {
      outDir: "dist",
      rollupOptions: {
        input: "./index.html",
      },
    },
    define: {
      // Expose the OFREP service URL to the client
      ...defineConfig,
    },
  };
});
