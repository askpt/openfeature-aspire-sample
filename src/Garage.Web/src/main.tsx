import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App.tsx";
import "./index.css";
import { initializeTelemetry } from "./telemetry";
import { initializeFeatureFlags } from "./featureFlags.ts";

// Initialize OpenTelemetry FIRST (if Aspire provides the endpoint)
const otelEndpoint = import.meta.env.VITE_OTEL_ENDPOINT;
const otelHeaders = import.meta.env.VITE_OTEL_HEADERS || "";
const otelResourceAttributes =
  import.meta.env.VITE_OTEL_RESOURCE_ATTRIBUTES || "";

if (otelEndpoint) {
  initializeTelemetry(otelEndpoint, otelHeaders, otelResourceAttributes);
}

// Initialize OpenFeature provider before rendering
const ofrepServiceUrl = import.meta.env.VITE_OFREP_SERVICE_URL || "";

if (ofrepServiceUrl) {
  initializeFeatureFlags(ofrepServiceUrl);
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
