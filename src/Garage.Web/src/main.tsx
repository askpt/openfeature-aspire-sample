import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App.tsx";
import "./index.css";
import { initializeTelemetry } from "./telemetry";
import { initializeFeatureFlags } from "./featureFlags.ts";

// Get Vite env vars (works in both dev and production with proxied endpoints)
// In production, we use relative URLs (empty string) proxied through nginx
// In development, Vite provides the full URLs

// Initialize OpenTelemetry FIRST (if Aspire provides the endpoint)
const otelEndpoint = import.meta.env.VITE_OTEL_ENDPOINT || "";
const otelHeaders = import.meta.env.VITE_OTEL_HEADERS || "";
const otelResourceAttributes =
  import.meta.env.VITE_OTEL_RESOURCE_ATTRIBUTES || "";

initializeTelemetry(otelEndpoint, otelHeaders, otelResourceAttributes);

// Initialize OpenFeature provider before rendering
const ofrepServiceUrl = import.meta.env.VITE_OFREP_SERVICE_URL || "";

initializeFeatureFlags(ofrepServiceUrl);

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
