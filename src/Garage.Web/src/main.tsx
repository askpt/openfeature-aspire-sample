import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App.tsx";
import "./index.css";
import { initializeTelemetry } from "./telemetry";
import { initializeFeatureFlags } from "./featureFlags.ts";

// Get runtime config (production) or Vite env vars (development)
const runtimeConfig = (window as any).__RUNTIME_CONFIG__;

// Initialize OpenTelemetry FIRST (if Aspire provides the endpoint)
const otelEndpoint =
  runtimeConfig?.VITE_OTEL_ENDPOINT || import.meta.env.VITE_OTEL_ENDPOINT;
const otelHeaders =
  runtimeConfig?.VITE_OTEL_HEADERS || import.meta.env.VITE_OTEL_HEADERS || "";
const otelResourceAttributes =
  runtimeConfig?.VITE_OTEL_RESOURCE_ATTRIBUTES ||
  import.meta.env.VITE_OTEL_RESOURCE_ATTRIBUTES ||
  "";

if (otelEndpoint) {
  initializeTelemetry(otelEndpoint, otelHeaders, otelResourceAttributes);
}

// Initialize OpenFeature provider before rendering
const ofrepServiceUrl =
  runtimeConfig?.VITE_OFREP_SERVICE_URL ||
  import.meta.env.VITE_OFREP_SERVICE_URL ||
  "";

if (ofrepServiceUrl) {
  initializeFeatureFlags(ofrepServiceUrl);
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
