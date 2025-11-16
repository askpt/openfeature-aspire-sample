import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");

  const ofrepServiceUrl = process.env.services__flagd__ofrep__0;
  const otelEndpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;
  const otelHeaders = process.env.OTEL_EXPORTER_OTLP_HEADERS;
  const otelResourceAttributes = process.env.OTEL_RESOURCE_ATTRIBUTES;

  // Only define service URLs and OTEL config if they are set
  const defineConfig: any = {};

  // For OFREP, use relative URL (proxied) instead of absolute URL
  defineConfig["import.meta.env.VITE_OFREP_SERVICE_URL"] = JSON.stringify(
    ofrepServiceUrl ? "/ofrep" : ""
  );

  if (otelEndpoint) {
    defineConfig["import.meta.env.VITE_OTEL_ENDPOINT"] =
      JSON.stringify(otelEndpoint);
  }
  if (otelHeaders) {
    defineConfig["import.meta.env.VITE_OTEL_HEADERS"] =
      JSON.stringify(otelHeaders);
  }
  if (otelResourceAttributes) {
    defineConfig["import.meta.env.VITE_OTEL_RESOURCE_ATTRIBUTES"] =
      JSON.stringify(otelResourceAttributes);
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
        "/ofrep": {
          target: ofrepServiceUrl,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/ofrep/, ""),
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
